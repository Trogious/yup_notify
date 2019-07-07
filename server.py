import logging
import os
import ssl
import subprocess
import sys
from queue import Queue
from threading import Thread

from flask import Flask, request

API_KEY = 'abc123'
UPLOADER_SCRIPT = './upload.sh'
SSL_CERT_PATH = './cert.pem'
SSL_KEY_PATH = './key.pem'


class NotificationManager(Thread):
    notifications = Queue()

    @staticmethod
    def initiate_upload():
        subprocess.run(UPLOADER_SCRIPT)

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
            NotificationManager.initiate_upload()
            NotificationManager.notifications.get()


def run_server():
    app = Flask(__name__)

    @app.route('/notify', methods=['POST'])
    def notified():
        try:
            if request.method == 'POST' and request.form.get('key') == API_KEY:
                NotificationManager.initiate(request.form.get('notification'))
                return 'notified', 200
        except Exception as e:
            sys.stderr.write(str(e) + '\n')
        return 'NotFound', 404
    app.logger.disabled = True
    logging.getLogger('werkzeug').disabled = True
    server_host = os.getenv('YUP_NOTIFY_HOST', '0.0.0.0')
    server_port = os.getenv('YUP_NOTIFY_PORT', 8000)
    context = ssl.SSLContext(ssl.PROTOCOL_TLSv1_2)
    context.load_cert_chain(SSL_CERT_PATH, SSL_KEY_PATH)
    app.run(host=server_host, port=server_port, ssl_context=context)


run_server()
