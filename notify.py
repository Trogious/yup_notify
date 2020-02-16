import os

import requests
import urllib3

NOTIFY_KEY = os.getenv('YUP_NOTIFY_API_KEY', 'abc123')
NOTIFY_URI = os.getenv('YUP_NOTIFY_URI', 'https://127.0.0.1:8000/notify')
NOTIFICATION = os.getenv('YUP_NOTIFY_NOTIFICATION', 'gameplay')

if __name__ == '__main__':
    urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)
    resp = requests.post(NOTIFY_URI, data={'key': NOTIFY_KEY, 'notification': NOTIFICATION}, verify=False)
    if resp.status_code == 200:
        print(resp.text)
    else:
        print('error: %d' % resp.status_code)
