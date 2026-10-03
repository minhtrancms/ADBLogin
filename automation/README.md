# ADBLogin Automation & Remote CDP Guide

Bộ công cụ tự động hóa ADBLogin cung cấp 2 hình thức:
1. **Giao diện tích hợp sẵn (C# / WinForms):** Bấm nút **🤖 Auto FB** trên thanh công cụ của ADBLogin.
2. **Cổng Remote DevTools (CDP) & Local REST API:** Cho phép điều khiển trình duyệt Orbita/Chrome qua Python, Node.js, Puppeteer, Playwright, Selenium.

---

## 1. Local REST API Endpoints (Mặc định Port 5858)

Khi mở ứng dụng ADBLogin, một máy chủ HTTP Server cục bộ sẽ tự động chạy tại: `http://127.0.0.1:5858`

| Phương thức | Endpoint | Ý nghĩa |
|---|---|---|
| `GET` | `/api/status` | Kiểm tra trạng thái máy chủ API và số browser đang mở |
| `GET` | `/api/profiles` | Lấy danh sách toàn bộ profile kèm trạng thái & CDP port |
| `GET` | `/api/profile/start?id={profileId}` | Khởi chạy profile và nhận về `cdp_port` |
| `GET` | `/api/profile/stop?id={profileId}` | Đóng profile đang chạy |
| `GET` | `/api/fb/check_uid?uid={uid}` | Kiểm tra nhanh UID Facebook Live hay Die |
| `POST` | `/api/fb/login_cookie` | Đăng nhập profile bằng Cookie |

---

## 2. Kết nối bằng Python Playwright qua CDP

```python
import requests
from playwright.sync_api import sync_playwright

# 1. Gọi API mở profile
res = requests.get("http://127.0.0.1:5858/api/profile/start?id=PROFILE_ID").json()
cdp_port = res["cdp_port"]

# 2. Kết nối Playwright vào cổng CDP
with sync_playwright() as p:
    browser = p.chromium.connect_over_cdp(f"http://127.0.0.1:{cdp_port}")
    page = browser.contexts[0].pages[0]
    page.goto("https://www.facebook.com/")
    print(page.title())
```

---

## 3. Kết nối bằng Python Selenium qua CDP

```python
import requests
from selenium import webdriver
from selenium.webdriver.chrome.options import Options

res = requests.get("http://127.0.0.1:5858/api/profile/start?id=PROFILE_ID").json()
cdp_port = res["cdp_port"]

options = Options()
options.add_experimental_option("debuggerAddress", f"127.0.0.1:{cdp_port}")
driver = webdriver.Chrome(options=options)
driver.get("https://www.facebook.com/")
```
