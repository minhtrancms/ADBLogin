"""
ADBLogin Automation API Client (Python)
Ket noi va dieu khien Antidetect Browser ADBLogin qua Local REST API & Chrome DevTools Protocol (CDP).
"""

import requests
import json
import time

API_BASE = "http://127.0.0.1:5858"

def check_api_status():
    """Kiem tra xem ADBLogin API server co dang chay khong"""
    try:
        res = requests.get(f"{API_BASE}/api/status", timeout=3)
        return res.json()
    except Exception as e:
        print(f"[-] Khong the ket noi toi ADBLogin API: {e}")
        return None

def get_profiles():
    """Lay danh sach tat ca profiles tren ADBLogin"""
    res = requests.get(f"{API_BASE}/api/profiles")
    return res.json().get("data", [])

def start_profile(profile_id):
    """
    Khoi chay profile, tra ve CDP port va duong dan WebSocket CDP
    de Playwright / Puppeteer / Selenium ket noi truc tiep.
    """
    res = requests.get(f"{API_BASE}/api/profile/start", params={"id": profile_id})
    data = res.json()
    if data.get("code") == 0:
        cdp_port = data.get("cdp_port")
        print(f"[+] Profile '{profile_id}' da mo tren CDP Port: {cdp_port}")
        return cdp_port
    else:
        print(f"[-] Khoi chay that bai: {data.get('message')}")
        return None

def stop_profile(profile_id):
    """Dong profile dang chay"""
    res = requests.get(f"{API_BASE}/api/profile/stop", params={"id": profile_id})
    print(f"[*] Ket qua dong: {res.json().get('message')}")

def check_fb_uid_live(uid):
    """Kiem tra nhanh UID Facebook con Live hay Die"""
    res = requests.get(f"{API_BASE}/api/fb/check_uid", params={"uid": uid})
    data = res.json()
    return data.get("is_live", False)

if __name__ == "__main__":
    print("=== ADBLogin Python Automation Demo ===")
    status = check_api_status()
    if not status:
        print("Vui long bat ADBLogin truoc khi chay script nay.")
        exit(1)

    print(f"[+] Ket noi thanh cong toi ADBLogin API (Port {status.get('port')})")
    profiles = get_profiles()
    print(f"[*] Tong so profiles: {len(profiles)}")

    if len(profiles) > 0:
        first = profiles[0]
        pid = first["profile_id"]
        pname = first["name"]
        print(f"[*] Thu mo profile dau tien: {pname} ({pid})")

        port = start_profile(pid)
        if port:
            print(f"[+] Gio ban co the dung Playwright hoac Selenium ket noi toi: http://127.0.0.1:{port}")
            time.sleep(5)
            # stop_profile(pid)
