import logging
import os
import ssl
import subprocess
import sys
from queue import Queue
from threading import Thread

from flask import Flask, request

API_KEY = os.getenv('YUP_API_KEY', 'abc123')
UPLOADER_SCRIPT_DIR = os.getenv('YUP_UPLOADER_SCRIPT_DIR', './')
UPLOADER_SCRIPT_DEFAULT = os.getenv('YUP_UPLOADER_SCRIPT_DEFAULT', 'uploadGameplay.sh')
SSL_CERT_PATH = os.getenv('YUP_SSL_CERT_PATH', './cert.pem')
SSL_KEY_PATH = os.getenv('YUP_SSL_KEY_PATH', './key.pem')
LISTEN_HOST = os.getenv('YUP_NOTIFY_HOST', '0.0.0.0')
LISTEN_PORT = int(os.getenv('YUP_NOTIFY_PORT', 8000))


class NotificationManager(Thread):
    notifications = Queue()

    @staticmethod
    def get_uploader_script(notification):
        name = 'upload%s%s.sh' % (notification[0].upper(), notification[1:])
        path = os.path.join(UPLOADER_SCRIPT_DIR, name)
        if os.path.isfile(path):
            return path
        sys.stderr.write('no script found for: %s\n' % notification)
        path = os.path.join(UPLOADER_SCRIPT_DIR, UPLOADER_SCRIPT_DEFAULT)
        if os.path.isfile(path):
            return path
        return None

    @staticmethod
    def initiate_upload(notification):
        path = NotificationManager.get_uploader_script(notification)
        if path:
            subprocess.run(path)
        else:
            sys.stderr.write('uploaders script not found\n')

    @staticmethod
    def initiate(notification):
        if NotificationManager.notifications.empty():
            NotificationManager.notifications.put(notification)
            thread = NotificationManager()
            thread.start()
        else:
            NotificationManager.notifications.put(notification)

    def __init__(self):
        super().__init__()

    def run(self):
        while not NotificationManager.notifications.empty():
            NotificationManager.initiate_upload(NotificationManager.notifications.get())


def run_server():
    app = Flask(__name__)

    @app.route('/notify', methods=['POST'])
    def notified():
        try:
            print(request.form)
            if request.method == 'POST' and request.form.get('key') == API_KEY:
                NotificationManager.initiate(request.form.get('notification'))
                return 'notified', 200
        except Exception as e:
            sys.stderr.write(str(e) + '\n')
        return 'NotFound', 404
    app.logger.disabled = True
    logging.getLogger('werkzeug').disabled = True
    context = ssl.SSLContext(ssl.PROTOCOL_TLSv1_2)
    context.load_cert_chain(SSL_CERT_PATH, SSL_KEY_PATH)
    app.run(host=LISTEN_HOST, port=LISTEN_PORT, ssl_context=context)


run_server()
