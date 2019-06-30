# This is a modified implementation of Pankaj Pandey's (pankajp) gist at https://gist.github.com/pankajp/280596a5dabaeeceaaaa
# which is itself a version of the python 2.x SimpleHTTPRequestHandler
# I moved this to the python 3.x version, made it send the header 'Accept-Range: bytes', use HTTP/1.1 by default and allowed to change the serving path (like in the original version)

__version_info__ = (1, 0, 0)
__version__ = '.'.join(map(str, __version_info__))
__author__ = "Sebastian Hofmann (Kaemmelot)"
__all__ = ['get_threaded_server', 'run_server',
           'ThreadingHTTPServer', 'RangeHTTPRequestHandler']

import datetime
import email.utils
import logging
import os
import re
import urllib.parse
from argparse import ArgumentParser
from errno import EADDRINUSE
from html import escape
from http import HTTPStatus
from http.server import HTTPServer, SimpleHTTPRequestHandler
from io import BytesIO
from posixpath import normpath
from signal import SIGINT, signal
from socket import AF_INET6
from socketserver import ThreadingMixIn
from sys import getfilesystemencoding
from threading import Semaphore, Thread


class ThreadingHTTPServer(ThreadingMixIn, HTTPServer):
    # TODO not nice, but no need to manually kill pending connections on shutdown
    daemon_threads = True


class RangeHTTPRequestHandler(SimpleHTTPRequestHandler):
    # TODO non static serve_path?
    serve_path = os.getcwd()
    server_version = "RangeHTTP/" + __version__
    # enable keepalives:
    protocol_version = "HTTP/1.1"

    range_regex = re.compile(r"^bytes=(\d+)\-(\d+)?")

    def do_HEAD(self):
        """ Overridden to handle HTTP Range requests.
        """
        self.range_from, self.range_to = self._get_range_header()
        self.response_type = HTTPStatus.OK if self.range_from == None else HTTPStatus.PARTIAL_CONTENT
        self.cors_req = self._has_cors_header()
        f = self.send_headers()
        # don't send the file
        if f:
            f.close()

    def do_GET(self):
        """ Overridden to handle HTTP Range requests.
        """
        self.range_from, self.range_to = self._get_range_header()
        self.response_type = HTTPStatus.OK if self.range_from == None else HTTPStatus.PARTIAL_CONTENT
        self.cors_req = self._has_cors_header()
        f = self.send_headers()
        if f:
            try:
                if self.range_from == None:
                    self.copyfile(f, self.wfile)  # default
                else:
                    self.copy_file_range(f, self.wfile)  # ranged
            finally:
                f.close()

    def do_OPTIONS(self):
        """ Added for CORS requests
        """
        # self.path
        self.response_type = HTTPStatus.NO_CONTENT
        self.cors_req = True
        self.send_response(self.response_type)
        self.send_header("Allow", "OPTIONS, GET, HEAD")
        self.send_cors_headers()
        self.end_headers()

    def handle_one_request(self):
        try:
            super().handle_one_request()
        except (ConnectionResetError, ConnectionAbortedError, BrokenPipeError):  # ignore closed connections
            logging.debug("Connection closed")
            self.close_connection = True

    def copy_file_range(self, in_file, out_file):
        """ Copy only the range in self.range_from/to.
        """
        in_file.seek(self.range_from)
        # Add 1 because the range is inclusive
        bytes_to_copy = 1 + self.range_to - self.range_from
        buf_length = 64*1024
        bytes_copied = 0
        while bytes_copied < bytes_to_copy:
            read_buf = in_file.read(
                min(buf_length, bytes_to_copy-bytes_copied))
            if len(read_buf) == 0:
                break
            out_file.write(read_buf)
            bytes_copied += len(read_buf)
        return bytes_copied

    def send_headers(self):
        path = self.translate_path(self.path)
        f = None
        if os.path.isdir(path):
            parts = urllib.parse.urlsplit(self.path)
            if not parts.path.endswith('/'):
                # redirect browser - doing basically what apache does
                self.send_response(HTTPStatus.MOVED_PERMANENTLY)
                new_parts = (parts[0], parts[1], parts[2] + '/',
                             parts[3], parts[4])
                new_url = urllib.parse.urlunsplit(new_parts)
                self.send_header("Location", new_url)
                self.end_headers()
                return None
            for index in "index.html", "index.htm":
                index = os.path.join(path, index)
                if os.path.exists(index):
                    path = index
                    break
            else:
                return self.list_directory(path)

        ctype = self.guess_type(path)
        try:
            f = open(path, 'rb')
        except OSError:
            self.send_error(HTTPStatus.NOT_FOUND, "File not found")
            return None

        try:
            fs = os.fstat(f.fileno())
            # Use browser cache if possible
            if "If-Modified-Since" in self.headers:
                # compare If-Modified-Since and time of last file modification
                try:
                    ims = email.utils.parsedate_to_datetime(
                        self.headers["If-Modified-Since"])
                except (TypeError, IndexError, OverflowError, ValueError):
                    # ignore ill-formed values
                    pass
                else:
                    if ims.tzinfo is None:
                        # obsolete format with no timezone, cf.
                        # https://tools.ietf.org/html/rfc7231#section-7.1.1.1
                        ims = ims.replace(tzinfo=datetime.timezone.utc)
                    if ims.tzinfo is datetime.timezone.utc:
                        # compare to UTC datetime of last modification
                        last_modif = datetime.datetime.fromtimestamp(
                            fs.st_mtime, datetime.timezone.utc)
                        # remove microseconds, like in If-Modified-Since
                        last_modif = last_modif.replace(microsecond=0)

                        if last_modif <= ims:
                            self.send_response(HTTPStatus.NOT_MODIFIED)
                            self.end_headers()
                            f.close()
                            return None

            self.send_response(self.response_type)

            # show that we allow range requests
            self.send_header("Accept-Ranges", "bytes")

            self.send_header("Content-Type", ctype)

            file_size = fs[6]
            self.send_length_header(file_size)
            self.send_header(
                "Last-Modified", self.date_time_string(fs.st_mtime))
            # self.send_header("Date", self.date_time_string(fs.st_mtime)) #dafuq?
            self.send_cors_headers()
            self.end_headers()
            return f
        except:
            f.close()
            raise

    def send_cors_headers(self):
        if self.cors_req:
            self.send_header("Access-Control-Allow-Origin", "*")
            self.send_header("Access-Control-Allow-Methods",
                             "OPTIONS, GET, HEAD")
            self.send_header("Access-Control-Allow-Headers",
                             "If-Modified-Since, Range")
            self.send_header("Access-Control-Expose-Headers",
                             "Content-Length, Content-Range, Accept-Ranges")
            self.send_header("Access-Control-Max-Age", "86400")

    def send_length_header(self, file_size):
        if self.range_from != None:
            if self.range_to == None or self.range_to >= file_size:
                self.range_to = file_size-1
            self.send_header("Content-Range",
                             "bytes %d-%d/%d" % (self.range_from,
                                                 self.range_to,
                                                 file_size))
            # Add 1 because ranges are inclusive
            self.send_header("Content-Length",
                             (1 + self.range_to - self.range_from))
            logging.debug("Range request, sending bytes %d-%d/%d" % (self.range_from,
                                                                     self.range_to,
                                                                     file_size))
        else:
            self.send_header("Content-Length", str(file_size))

    def list_directory(self, path):
        """Helper to produce a directory listing (absent index.html).

        Return value is either a file object, or None (indicating an
        error).  In either case, the headers are sent, making the
        interface the same as for send_headers().
        """
        try:
            list = os.listdir(path)
        except OSError:
            self.send_error(
                HTTPStatus.NOT_FOUND,
                "No permission to list directory")
            return None
        list.sort(key=lambda a: a.lower())
        logging.debug("Listing directory %s" % list)
        r = []
        try:
            displaypath = urllib.parse.unquote(self.path,
                                               errors='surrogatepass')
        except UnicodeDecodeError:
            displaypath = urllib.parse.unquote(path)
        displaypath = escape(displaypath)
        enc = getfilesystemencoding()
        title = 'Directory listing for %s' % displaypath
        r.append('<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.01//EN" '
                 '"http://www.w3.org/TR/html4/strict.dtd">')
        r.append('<html>\n<head>')
        r.append('<meta http-equiv="Content-Type" '
                 'content="text/html; charset=%s">' % enc)
        r.append('<title>%s</title>\n</head>' % title)
        r.append('<body>\n<h1>%s</h1>' % title)
        r.append('<hr>\n<ul>')
        for name in list:
            fullname = os.path.join(path, name)
            displayname = linkname = name
            # Append / for directories or @ for symbolic links
            if os.path.isdir(fullname):
                displayname = name + "/"
                linkname = name + "/"
            if os.path.islink(fullname):
                displayname = name + "@"
                # Note: a link to a directory displays with @ and links with /
            r.append('<li><a href="%s">%s</a></li>'
                     % (urllib.parse.quote(linkname,
                                           errors='surrogatepass'),
                        escape(displayname)))
        r.append('</ul>\n<hr>\n</body>\n</html>\n')
        encoded = '\n'.join(r).encode(enc, 'surrogateescape')
        f = BytesIO()
        f.write(encoded)
        f.seek(0)
        self.send_response(HTTPStatus.OK)
        # show that we allow range requests
        self.send_header("Accept-Ranges", "bytes")
        self.send_header("Content-Type", "text/html; charset=%s" % enc)
        self.send_header("Content-Length", str(len(encoded)))
        self.send_cors_headers()
        self.end_headers()
        return f

    def translate_path(self, path):
        """Translate a /-separated PATH to the local filename syntax.

        Components that mean special things to the local file system
        (e.g. drive or directory names) are ignored.  (XXX They should
        probably be diagnosed.)
        """
        # abandon query parameters
        path = path.split('?', 1)[0]
        path = path.split('#', 1)[0]
        # Don't forget explicit trailing slash when normalizing. Issue17324
        trailing_slash = path.rstrip().endswith('/')
        try:
            path = urllib.parse.unquote(path, errors='surrogatepass')
        except UnicodeDecodeError:
            path = urllib.parse.unquote(path)
        path = normpath(path)
        words = path.split('/')
        words = filter(None, words)
        path = self.serve_path  # use own path here (no cwd)
        for word in words:
            if os.path.dirname(word) or word in (os.curdir, os.pardir):
                # Ignore components that are not a simple file/directory name
                continue
            path = os.path.join(path, word)
        if trailing_slash:
            path += '/'
        return path

    # Private interface ######################################################

    def _has_cors_header(self):
        """Returns if a CORS relevant header was used in the request.

        In that case the answer should include apropiate headers.
        """
        return "Access-Control-Request-Method" in self.headers or "Access-Control-Request-Headers" in self.headers or "Origin" in self.headers

    def _get_range_header(self):
        """Returns request Range start and end if specified.

        If Range header is not specified returns (None, None)
        """
        try:
            range_header = self.headers["Range"]
            if range_header == None:
                return (None, None)
            match = self.range_regex.match(range_header)
            if match == None:
                return (None, None)
        except:
            return (None, None)
        from_val = int(match.group(1))
        if match.group(2) != None:
            return (from_val, int(match.group(2)))
        else:
            return (from_val, None)


def get_threaded_server(port=8080, next_attempts=0, serve_path=None, ipv6=False, handler=RangeHTTPRequestHandler):
    if serve_path:
        handler.serve_path = serve_path
    while next_attempts >= 0:
        try:
            httpd = ThreadingHTTPServer(("localhost", port), handler)
            if (ipv6):
                httpd.address_family = AF_INET6
            return httpd
        except OSError as e:
            if e.errno == EADDRINUSE:
                next_attempts -= 1
                port += 1
            else:
                raise


runSema = Semaphore(0)


def shutdown_handler(a, b):
    logging.debug("Registered shutdown request")
    runSema.release()


def run_server(server, sema=None):
    try:
        server.serve_forever()
    except:
        pass  # ignore errors on shutdown
    if sema != None:
        sema.release()  # in case another error occurred


def main():
    """Host server from console.
    """
    signal(SIGINT, shutdown_handler)
    parser = ArgumentParser()
    parser.add_argument("-p", "--port", help="The port to run the server on (Default: 8080)",
                        type=int, default=8080, required=False)
    parser.add_argument("-d", "--dir", help="The directory to host (Default: current directory)",
                        type=str, default=os.getcwd(), required=False)
    parser.add_argument(
        "-6", "--ipv6", help="Use IPv6 instead of IPv4", action='store_true')
    args = parser.parse_args()

    httpd = get_threaded_server(
        port=args.port, serve_path=args.dir, ipv6=args.ipv6)

    logging.info("Serving %s at localhost:%d via IPv%d..." %
                 (args.dir, args.port, 6 if args.ipv6 else 4))
    Thread(target=run_server, name="threaded_http_server", kwargs={
        "server": httpd, "sema": runSema}).start()
    while not runSema.acquire(True, 0.25):
        pass
    logging.info("Shutting down")
    httpd.server_close()


if __name__ == "__main__":
    main()
