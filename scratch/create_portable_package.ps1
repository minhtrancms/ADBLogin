$ErrorActionPreference = "Stop"

$workspace = "D:\ADBLogin_V111_Pass_999"
$stagingBase = "D:\ADBLogin_Portable_Temp"
$stagingApp = "D:\ADBLogin_Portable_Temp\ADBLogin"
$zipOutput = "D:\ADBLogin_v2.0_Portable.zip"

Write-Host "=== 1. Don dep thu muc staging va file zip cu ===" -ForegroundColor Cyan
if (Test-Path $stagingBase) {
    Remove-Item -Path $stagingBase -Recurse -Force
}
if (Test-Path $zipOutput) {
    Remove-Item -Path $zipOutput -Force
}

New-Item -ItemType Directory -Path $stagingApp -Force | Out-Null

Write-Host "=== 2. Sao chep Executable va Chromedriver ===" -ForegroundColor Cyan
Copy-Item "$workspace\ADBLogin.exe" "$stagingApp\" -Force
Copy-Item "$workspace\ADBLogin_Update.exe" "$stagingApp\" -Force

$chromedriverSrc = "C:\Users\Admin\.cache\selenium\chromedriver\win64\144.0.7559.133\chromedriver.exe"
if (Test-Path $chromedriverSrc) {
    Copy-Item $chromedriverSrc "$stagingApp\chromedriver.exe" -Force
    Write-Host "  -> Da copy chromedriver 144 truc tiep vao thu muc goc ung dung" -ForegroundColor Green
}

Write-Host "=== 3. Sao chep cac file DLL Runtime ===" -ForegroundColor Cyan
$dlls = @(
    "AE.Net.Mail.dll",
    "Faker.dll",
    "HtmlAgilityPack.dll",
    "Leaf.xNet.dll",
    "Microsoft.Bcl.AsyncInterfaces.dll",
    "Newtonsoft.Json.dll",
    "Otp.NET.dll",
    "System.Buffers.dll",
    "System.Drawing.Common.dll",
    "System.IO.Pipelines.dll",
    "System.Memory.dll",
    "System.Numerics.Vectors.dll",
    "System.Runtime.CompilerServices.Unsafe.dll",
    "System.Text.Encodings.Web.dll",
    "System.Text.Json.dll",
    "System.Threading.Tasks.Extensions.dll",
    "WebDriver.dll",
    "WooCommerce.NET.dll"
)

foreach ($dll in $dlls) {
    $src = "$workspace\$dll"
    if (Test-Path $src) {
        Copy-Item $src "$stagingApp\" -Force
    } else {
        Write-Warning "Khong tim thay $dll"
    }
}

$netstandardSrc = "C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\Facades\netstandard.dll"
if (Test-Path $netstandardSrc) {
    Copy-Item $netstandardSrc "$stagingApp\netstandard.dll" -Force
    Write-Host "  -> Da copy netstandard.dll ho tro .NET 4.8" -ForegroundColor Green
}

Write-Host "=== 4. Sao chep Selenium Manager ===" -ForegroundColor Cyan
$smDir = "$stagingApp\selenium-manager\windows"
New-Item -ItemType Directory -Path $smDir -Force | Out-Null
Copy-Item "$workspace\selenium-manager\windows\selenium-manager.exe" "$smDir\" -Force

Write-Host "=== 5. Sao chep Files (UserAgent, Proxy, zero_profile) ===" -ForegroundColor Cyan
$filesTarget = "$stagingApp\Files"
New-Item -ItemType Directory -Path "$filesTarget\Profiles" -Force | Out-Null
Copy-Item "$workspace\Files\UserAgent.txt" "$filesTarget\" -Force
Copy-Item "$workspace\Files\Proxy.txt" "$filesTarget\" -Force
Copy-Item "$workspace\Files\zero_profile" "$filesTarget\" -Recurse -Force

Write-Host "=== 6. Sao chep Extensions va chrome-extensions ===" -ForegroundColor Cyan
Copy-Item "$workspace\Extensions" "$stagingApp\" -Recurse -Force
if (Test-Path "$workspace\chrome-extensions") {
    Copy-Item "$workspace\chrome-extensions" "$stagingApp\" -Recurse -Force
}

Write-Host "=== 7. Sao chep Gologin va Loi trinh duyet Orbita 144 ===" -ForegroundColor Cyan
$gologinTarget = "$stagingApp\Gologin"
New-Item -ItemType Directory -Path "$gologinTarget\All-Browsers" -Force | Out-Null
Copy-Item "$workspace\Gologin\extensions" "$gologinTarget\" -Recurse -Force

$orbitaSrc = "$env:USERPROFILE\.gologin\browser\orbita-browser-144"
if (Test-Path $orbitaSrc) {
    Write-Host "  -> Dang copy loi Orbita 144 (co the mat 10-15 giay)..." -ForegroundColor Yellow
    Copy-Item $orbitaSrc "$gologinTarget\orbita-browser-144" -Recurse -Force
    # Xoa log neu co
    if (Test-Path "$gologinTarget\orbita-browser-144\debug.log") {
        Remove-Item "$gologinTarget\orbita-browser-144\debug.log" -Force
    }
    Write-Host "  -> Da copy xong Orbita 144" -ForegroundColor Green
} else {
    Write-Warning "Khong tim thay $orbitaSrc"
}

$readmeAllBrowsers = @"
Thu muc chua cac ban cai dat trinh duyet bo sung (neu can).
Ban co the dat cac file orbita-browser-{version}.zip vao day.
Phien ban mac dinh Orbita 144 da duoc tich hop san trong thu muc Gologin/orbita-browser-144.
"@
$readmeAllBrowsers | Out-File -FilePath "$gologinTarget\All-Browsers\Readme.txt" -Encoding UTF8

Write-Host "=== 8. Sao chep Automation ===" -ForegroundColor Cyan
if (Test-Path "$workspace\automation") {
    Copy-Item "$workspace\automation" "$stagingApp\" -Recurse -Force
}

Write-Host "=== 9. Cau hinh config.json va profiles.json ===" -ForegroundColor Cyan
$configContent = @"
{
  "IsOfflineMode": true,
  "SkipServerAuthentication": true,
  "AllowAutoUpdateCheck": false,
  "LicenseKey": "STANDALONE_LIFETIME_ACCESS",
  "LicenseStatus": "Activated",
  "PlanName": "Enterprise Unlimited",
  "ActivationDate": "2026-10-03T00:00:00",
  "SuppressNotificationPopups": true,
  "EnableCoffeePromotionPopup": false,
  "ShowFreeTierWarnings": false,
  "ProfilesDirectory": "Files/zero_profile",
  "ProxiesFilePath": "Files/Proxy.txt",
  "UserAgentFilePath": "Files/UserAgent.txt",
  "MaxConcurrentBrowsers": 50,
  "SelectedBrowserVersion": "Orbita 144",
  "CustomBrowserPath": ""
}
"@
$configContent | Out-File -FilePath "$stagingApp\config.json" -Encoding UTF8

# profiles.json mau (2 demo profile sach)
$starterProfiles = @"
[
  {
    "ProfileId": "demo00010001000100010001",
    "ProfileName": "Profile 01 - Demo",
    "Username": "demo1@adblogin.local",
    "Tier": 2,
    "IsActive": true,
    "CreatedDate": "2026-10-03T12:00:00",
    "ExpiryDate": null,
    "Proxy": "",
    "UserAgent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/144.0.0.0 Safari/537.36",
    "BrowserPath": "",
    "BrowserVersion": "Orbita 144",
    "Notes": "Profile khoi tao mau"
  },
  {
    "ProfileId": "demo00020002000200020002",
    "ProfileName": "Profile 02 - Demo",
    "Username": "demo2@adblogin.local",
    "Tier": 2,
    "IsActive": true,
    "CreatedDate": "2026-10-03T12:00:00",
    "ExpiryDate": null,
    "Proxy": "",
    "UserAgent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/144.0.0.0 Safari/537.36",
    "BrowserPath": "",
    "BrowserVersion": "Orbita 144",
    "Notes": "Profile khoi tao mau"
  }
]
"@
$starterProfiles | Out-File -FilePath "$stagingApp\profiles.json" -Encoding UTF8

# Copy profiles cu lam backup
Copy-Item "$workspace\profiles.json" "$stagingApp\profiles_original_backup.json" -Force

Write-Host "=== 10. Tao Huong_Dan_Su_Dung.txt ===" -ForegroundColor Cyan
$huongDan = @"
========================================================================
            HUONG DAN SU DUNG ADBLOGIN ANTIDETECT V2.0 (PORTABLE)
========================================================================

Ban chi can giai nen file ZIP ra bat ky o dia nao (vi du: D:\ADBLogin hoac Desktop)
va chay truc tiep file ADBLogin.exe la su dung duoc ngay! Khong can cai dat gi them!

1. KHOI DONG:
   - Nhan dup chuot vao: ADBLogin.exe
   - Ung dung da duoc kich hoat san ban quyen vinh vien (Enterprise Unlimited).
   - Che do Offline Mode tu dong hoat dong 100%, khong yeu cau mang noi bo xac thuc.

2. LOI TRINH DUYET ANTIDETECT ORBITA 144:
   - Tool da tich hop san loi Orbita 144 (Chromium 144 moi nhat) trong thu muc:
     Gologin/orbita-browser-144/
   - Da tich hop san chromedriver 144 tuong thich 100%.
   - Tranh hoan toan cac canh bao trinh duyet cu cua Google/Gmail.

3. CAC CHUC NANG AUTOMATION VA MMO TICH HOP SAN:
   - Quan ly Profile: Chon profile tren danh sach va nhan nut [Mo Trinh Duyet] o thanh cong cu.
   - Mo Profile Mobile: Nhan chuot phai vao profile -> Chon [Mo dang Phone (Mobile Phone Farm)]
     (Man hinh ti le doc chuan Phone Farm).
   
   - [NEW] AUTOMATION SHOPEE: Nhan nut [Shopee Auto] hoac Menu Chuc Nang:
     + Diem danh nhan Shopee Xu moi ngay.
     + Luot Shopee Video tu nhien kem xem thoi gian random.
     + Tu dong luu / claim toan bo ma giam gia (Voucher Hunter) tai trung tam voucher.
     + Seeding tim kiem san pham / shop: Tim kiem tu khoa Spintax, cuon xem danh gia review,
       va tu dong them san pham vao gio hang tao luot quan tam (Add to cart seeding).
     + Dang nhap bang Cookie va Export Cookie Shopee.

   - [NEW] AUTOMATION X / TWITTER: Nhan nut [Sieu Tool] -> [X / Twitter Auto]:
     + Luot Home Timeline tu nhien voi toc do va cuon trang ngau nhien.
     + Tu dong Tha Tim (Like) va Retweet bai viet tren timeline.
     + Tu dong Follow tai khoan muc tieu theo danh sach username / profile URL.
     + Dang bai Tweet moi voi noi dung ho tro cu phap Spintax {A|B|C} tranh spam.
     + Dang nhap Cookie va Export Cookie Twitter.

   - [NEW] AUTOMATION TELEGRAM WEB: Nhan nut [Sieu Tool] -> [Telegram Auto]:
     + Tu dong tham gia (Join) vao cac kenh / nhom Telegram qua duong link t.me/...
     + Tu dong tha bieu cam (Reaction: Tim, Like, Fire...) vao tin nhan moi nhat cua Channel.
     + Kiem tra trang thai phien dang nhap Telegram Web.

   - [NEW] DONG BO THAO TAC (MULTI-CONTROL SYNCHRONIZER): Nhan nut [Sieu Tool] -> [Dong Bo Trinh Duyet]:
     + Chi dinh 1 Profile lam "Master" (Chu).
     + Chon hang loat Profile khac lam "Slaves" (Quan).
     + Dong bo tuc thi: Chuyen URL dong loat, Cuon trang (Scroll up/down), Nhap van ban dong thoi,
       Click chuot theo vi tri phan tram man hinh tren tat ca cac cua so slave cung mot luc!

   - [NEW] CAU HINH PROXY ROTATOR & CAPTCHA SOLVER: Nhan nut [Sieu Tool] -> [Cai Dat Proxy / Captcha]:
     + Tich hop san API Key xoay Proxy dong: TMProxy, Tinsoft, ShopLike, ProxyFB, va Custom API.
     + Tich hop san dich vu tu dong giai Captcha (CapSolver, 2Captcha, Anti-Captcha, 1stCaptcha).

   - AUTOMATION GOOGLE & GMAIL: Nhan nut [Auto Google] tren thanh cong cu hoac chuot phai:
     + Dang nhap Google tu dong vuot 2FA TOTP (tu dong giai ma Secret Key ra ma 6 so) & Recovery Email.
     + Google Search Seeding: Tim kiem tu khoa Spintax, cuon trang, bam vao web dich, doc bai tang SEO.
     + Xem video YouTube tu nhien & auto Tha Like tao lich su xem chan that cho tai khoan.
     + Doc Hop thu den Gmail: Tu dong mo thu va trich xuat ma OTP xac nhan (4-8 so).
     + Trich xuat Cookie Google vao Clipboard.

   - AUTOMATION TIKTOK: Nhan nut [Auto TikTok] tren thanh cong cu hoac chuot phai:
     + Nuoi nick luot FYP (For You Page): Cuon phim mui ten xuong tu nhien, thoi gian xem random,
       ty le tha tim ngau nhien va mo xem binh luan.
     + Auto Follow kenh theo danh sach profile / link channel.
     + Binh luan Video bang noi dung Spintax da dang tranh trung lap.
     + Dang nhap qua Cookie (SessionID) hoac kiem tra thong ke Follower/Likes/Status.

   - AUTOMATION FACEBOOK: Nhan nut [Auto FB] tren thanh cong cu hoac chuot phai:
     + Ho tro Nuoi nick: Luot Feed tu nhien, xem Reels, tuong tac ngau nhien.
     + Dang nhap tu dong bang Cookie hoac UID|Pass|2FA (Tu giai ma ma OTP 6 so).
     + Check Live / Die / Checkpoint.
     + Export Cookie, Access Token EAAB.

   - Tao Profile hang loat: Nhan nut [Tao Hang Loat] o thanh cong cu tren cung.

4. AUTOMATION QUA API / CDP (NEU CAN):
   - Tool tu dong khoi chay Local REST API Server tai cong: http://127.0.0.1:5858
   - Ho tro cac lenh:
     + GET  /api/profiles       : Lay danh sach profile
     + POST /api/profiles/start : Mo profile (kem mo cong CDP remote debugging)
     + POST /api/profiles/stop  : Dong profile
   - Chi tiet co trong thu muc: automation/

Chuc ban su dung hieu qua va thanh cong!
========================================================================
"@
$huongDan | Out-File -FilePath "$stagingApp\Huong_Dan_Su_Dung.txt" -Encoding UTF8
$huongDan | Out-File -FilePath "$workspace\Huong_Dan_Su_Dung.txt" -Encoding UTF8

Write-Host "=== 11. Nen thu muc sang file ZIP: $zipOutput ===" -ForegroundColor Cyan
Write-Host "  -> Dang nen file ZIP (co the mat 1-2 phut tuy toc do CPU)..." -ForegroundColor Yellow

Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($stagingBase, $zipOutput, [System.IO.Compression.CompressionLevel]::Optimal, $false)

$zipItem = Get-Item $zipOutput
$zipSizeMb = [math]::Round($zipItem.Length / 1MB, 2)
Write-Host "=== HOAN TAT! ===" -ForegroundColor Green
Write-Host "File ZIP tao tai: $zipOutput" -ForegroundColor Green
Write-Host "Dung luong ZIP : $zipSizeMb MB" -ForegroundColor Green

# Don dep staging base de tiet kiem o dia
Remove-Item -Path $stagingBase -Recurse -Force
Write-Host "Da don dep thu muc tam." -ForegroundColor Cyan
