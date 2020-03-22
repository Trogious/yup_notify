import json
import logging
import os
import ssl
import subprocess
import sys
import tempfile
from queue import Queue
from threading import Thread

from flask import Flask, request

API_KEY = os.getenv('YUP_API_KEY', 'abc123')
UPLOADER_SCRIPT_DIR = os.getenv('YUP_UPLOADER_SCRIPT_DIR', './')
UPLOADER_SCRIPT_DEFAULT = os.getenv('YUP_UPLOADER_SCRIPT_DEFAULT', 'uploadGameplay.sh')
UPLOADER_CONFIG_DEFAULT = os.getenv('YUP_UPLOADER_CONFIG_DEFAULT', 'gameplay_cfg.json')
UPLOADER_BIN_NAME = os.getenv('YUP_UPLOADER_BIN_NAME', 'yup.py')
SSL_CERT_PATH = os.getenv('YUP_SSL_CERT_PATH', './cert.pem')
SSL_KEY_PATH = os.getenv('YUP_SSL_KEY_PATH', './key.pem')
LISTEN_HOST = os.getenv('YUP_NOTIFY_HOST', '0.0.0.0')
LISTEN_PORT = int(os.getenv('YUP_NOTIFY_PORT', 8000))


def log(txt):
    sys.stderr.write(str(txt) + '\n')
    sys.stderr.flush()


class Notification:
    def __init__(self, value, is_script=True):
        self.value = value
        self.is_script = is_script


class NotificationManager(Thread):
    notifications = Queue()

    @staticmethod
    def process_notification(notification):
        if notification.is_script:
            NotificationManager.initiate_upload_script(notification.value)
        else:
            NotificationManager.initiate_upload_config(notification.value)

    @staticmethod
    def get_uploader_script(script_name):
        name = 'upload%s.sh' % (script_name.upper())
        path = os.path.join(UPLOADER_SCRIPT_DIR, name)
        if os.path.isfile(path):
            return path
        log('no script found for: %s' % script_name)
        path = os.path.join(UPLOADER_SCRIPT_DIR, UPLOADER_SCRIPT_DEFAULT)
        if os.path.isfile(path):
            return path
        return None

    @staticmethod
    def initiate_upload_script(script_name):
        path = NotificationManager.get_uploader_script(script_name)
        if path:
            subprocess.run(path)
        else:
            log('uploaders script not found')

    @staticmethod
    def initiate_upload_config(playlist_name):
        config_template_path = os.path.join(UPLOADER_SCRIPT_DIR, UPLOADER_CONFIG_DEFAULT)
        if not os.path.isfile(config_template_path):
            log('config template does not exist: %s' % config_template_path)
            return
        uploader_bin_path = os.path.join(UPLOADER_SCRIPT_DIR, UPLOADER_BIN_NAME)
        if not os.path.isfile(uploader_bin_path):
            log('uploader bin does not exist: %s' % uploader_bin_path)
            return
        try:
            with open(config_template_path, 'r') as in_cfg:
                config = json.load(in_cfg)
                config['playlist'] = playlist_name
                with tempfile.NamedTemporaryFile(mode='w') as out_cfg:
                    json.dump(config, out_cfg)
                    out_cfg.flush()
                    proc = subprocess.run([uploader_bin_path, out_cfg.name])
                    if proc.returncode != 0:
                        log('error executing uploader bin, code: %d' % proc.returncode)
        except Exception as e:
            log(e)

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
            NotificationManager.process_notification(NotificationManager.notifications.get())


def run_server():
    app = Flask(__name__)

    @app.route('/notify', methods=['POST'])
    def notified():
        try:
            if request.method == 'POST' and request.form.get('key') == API_KEY:
                NotificationManager.initiate(Notification(request.form.get('notification')))
                return 'notified', 200
        except Exception as e:
            log(e)
        return 'NotFound', 404

    @app.route('/notify_playlist', methods=['POST'])
    def notified_playlist():
        try:
            if request.method == 'POST' and request.form.get('key') == API_KEY:
                NotificationManager.initiate(Notification(request.form.get('notification'), False))
                return 'notified', 200
        except Exception as e:
            log(e)
        return 'NotFound', 404
    app.logger.disabled = True
    logging.getLogger('werkzeug').disabled = True
    context = ssl.SSLContext(ssl.PROTOCOL_TLSv1_2)
    context.load_cert_chain(SSL_CERT_PATH, SSL_KEY_PATH)
    log('PID: %d' % os.getpid())
    app.run(host=LISTEN_HOST, port=LISTEN_PORT, ssl_context=context)


run_server()
