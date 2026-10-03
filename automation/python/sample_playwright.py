"""
Ket noi Playwright vao Profile ADBLogin qua Chrome DevTools Protocol (CDP)
Cach cai dat: pip install playwright requests
"""

import time
import requests
from playwright.sync_api import sync_playwright

API_BASE = "http://127.0.0.1:5858"

def main():
    # 1. Lay danh sach profile va bat profile dau tien
    res = requests.get(f"{API_BASE}/api/profiles").json()
    profiles = res.get("data", [])
    if not profiles:
        print("Khong co profile nao tren ADBLogin!")
        return

    target_profile = profiles[0]
    pid = target_profile["profile_id"]
    print(f"[*] Dang khoi chay profile: {target_profile['name']} ({pid})...")

    start_res = requests.get(f"{API_BASE}/api/profile/start", params={"id": pid}).json()
    cdp_port = start_res.get("cdp_port")
    print(f"[+] CDP Port: {cdp_port}")

    # 2. Ket noi Playwright vao trinh duyet Orbita cua ADBLogin qua CDP
    with sync_playwright() as p:
        browser = p.chromium.connect_over_cdp(f"http://127.0.0.1:{cdp_port}")
        context = browser.contexts[0]
        page = context.pages[0] if context.pages else context.new_page()

        print("[*] Playwright dang dieu khien trinh duyet ADBLogin...")
        page.goto("https://www.facebook.com/")
        time.sleep(3)

        print(f"[+] Tieu de trang: {page.title()}")

        # Lướt nhẹ trang Facebook
        for i in range(3):
            page.mouse.wheel(0, 500)
            time.sleep(2)

        print("[+] Hoan tat test Playwright automation!")
        # browser.close()

if __name__ == "__main__":
    main()
