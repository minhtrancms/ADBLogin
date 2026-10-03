"""
Ket noi Selenium Python vao Profile ADBLogin qua Chrome DevTools Protocol (CDP)
Cach cai dat: pip install selenium requests
"""

import time
import requests
from selenium import webdriver
from selenium.webdriver.chrome.options import Options

API_BASE = "http://127.0.0.1:5858"

def main():
    # 1. Goi API bat profile
    res = requests.get(f"{API_BASE}/api/profiles").json()
    profiles = res.get("data", [])
    if not profiles:
        print("Khong co profile nao!")
        return

    pid = profiles[0]["profile_id"]
    start_res = requests.get(f"{API_BASE}/api/profile/start", params={"id": pid}).json()
    cdp_port = start_res.get("cdp_port")
    print(f"[+] CDP Port: {cdp_port}")

    # 2. Ket noi Selenium vao port devtools
    options = Options()
    options.add_experimental_option("debuggerAddress", f"127.0.0.1:{cdp_port}")

    driver = webdriver.Chrome(options=options)
    print(f"[+] Selenium da ket noi vao trinh duyet!")
    driver.get("https://www.facebook.com/")
    time.sleep(3)
    print(f"[+] Trang hien tai: {driver.title}")

if __name__ == "__main__":
    main()
