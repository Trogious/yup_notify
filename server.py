import logging
import os
import ssl
import subprocess
import sys

from flask import Flask, request

SECRET_KEY = 'abc123'
UPLOADER_SCRIPT = './upload.sh'
SSL_CERT_PATH = './cert.pem'
SSL_KEY_PATH = './key.pem'


def initiate_upload():
    subprocess.call(UPLOADER_SCRIPT)


def run_server():
    app = Flask(__name__)

    @app.route('/notify', methods=['POST'])
    def notified():
        try:
            if request.method == 'POST' and request.form.get('key') == SECRET_KEY:
                initiate_upload()
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
