# NovelTranslator (โปรแกรมแปลนิยายด้วย AI)

โปรแกรมเดสก์ท็อปสำหรับแปลนิยาย (อังกฤษ → ไทย) รองรับโมเดล AI ผ่าน OpenAI-compatible API (Ollama / LM Studio / Cloud Provider) พร้อมระบบบันทึกฐานข้อมูล SQLite, พจนานุกรมคำศัพท์ (Glossary), ค้นหา-แทนที่, และแบ่งชิ้นแปลอัจฉริยะ (Chunking & Resume)

พัฒนาด้วย **C# (.NET 10 LTS)** และ **Avalonia UI 11/12 (MVVM)**

---

## ฟีเจอร์หลัก (Phase 1 - Phase 6)

1. **AI Streaming & Resume อัจฉริยะ**:
   - แบ่งย่อหน้าเป็นส่วนย่อย (~600 คำ) ไม่ตัดกลางประโยคหรือบทสนทนา
   - แปลทีละส่วนแบบสตรีมมิ่ง (SSE) พร้อมระบบ **Buffered UI Streaming (~50ms)** ป้องกันหน้าจอกระตุก
   - บันทึกสถานะรายส่วนลงฐานข้อมูล SQLite ทันที หากกดหยุดหรือไฟดับ สามารถกด **"แปล / แปลต่อ"** เพื่อทำต่อจากจุดเดิมได้ทันที
2. **Output Validator & Auto-Clean**:
   - ตัดคำเกริ่นของ AI เช่น *"นี่คือคำแปล:"* อัตโนมัติด้วยโค้ดก่อน
   - ตรวจสอบความสมบูรณ์ ความยาว และตัวอักษรภาษาอังกฤษที่หลงเหลือ (ลองใหม่ได้สูงสุด 2 ครั้ง)
3. **Glossary (พจนานุกรมเฉพาะเรื่อง)**:
   - บันทึกคำศัพท์เฉพาะ (ชื่อตัวละคร, สถานที่, ท่าไม้ตาย) แยกตามเรื่อง
   - คัดกรองเฉพาะคำที่ปรากฏในชิ้นข้อความนั้นส่งเข้า AI เพื่อประหยัด Token และ VRAM
   - รองรับการ นำเข้า (Import) และ ส่งออก (Export) ไฟล์ `.csv`
   - ปุ่มลัด **"เพิ่มเข้า Glossary"** จากข้อความที่ลากคลุม
4. **ค้นหาและแทนที่คำแปล (Search & Replace)**:
   - ค้นหาคำในคำแปลและแสดงตัวอย่างก่อนเปลี่ยนจริง
   - เลือกระดับขอบเขตได้ทั้ง *"เฉพาะตอนนี้"* หรือ *"ทั้งเรื่อง (ทุกตอน)"*
5. **แปลซ้ำเฉพาะย่อหน้า (Retranslate Paragraph)**:
   - คลิกเลือกย่อหน้าที่ต้องการแล้วกดปุ่ม **"แปลซ้ำย่อหน้านี้"** เพื่อแปลใหม่เฉพาะจุด
6. **การจัดการไฟล์และฐานข้อมูล (Export / Import)**:
   - นำเข้าไฟล์ `.txt` / `.epub` (แยกตอนอัตโนมัติ)
   - ส่งออกตอนเดียวหรือทั้งเรื่องเป็น `.txt`, `.md`, `.epub`
   - สำรองและกู้คืนฐานข้อมูล SQLite (`.db`) ได้อย่างปลอดภัย
7. **เปรียบเทียบโมเดล (Model Comparison)**:
   - แปลข้อความตัวอย่างเดียวกันด้วย 2 โมเดลพร้อมกันเพื่อเทียบสำนวนและเลือกโมเดลที่เหมาะสม
8. **First-Run & Installer**:
   - ตรวจหา Ollama / LM Studio อัตโนมัติเมื่อเปิดใช้งานครั้งแรก พร้อมตัวช่วยตั้งค่า Cloud AI
   - รองรับการ Build แบบ Self-Contained และสร้างตัวติดตั้งด้วย Velopack / Inno Setup

---

## วิธีการเปิดใช้งานและรันโปรแกรม

### 1. ความต้องการของระบบ
- ติดตั้ง **.NET 10 SDK** ขึ้นไป
- โปรแกรม AI Local เช่น **Ollama** หรือ **LM Studio** (หรือจะใช้ OpenAI API Key ก็ได้)

### 2. คำสั่งรันโปรแกรม
เปิด Terminal / PowerShell ที่โฟลเดอร์นี้ แล้วพิมพ์คำสั่ง:
```bash
dotnet run --project src/NovelTranslator.App
```

### 3. คำสั่งรัน Unit Tests
```bash
dotnet test tests/NovelTranslator.Tests/NovelTranslator.Tests.csproj --no-build
```

---

## ขั้นตอนการใช้งานครั้งแรก

1. เปิดโปรแกรมขึ้นมา จะพบหน้าต่างหลักพร้อมนิยายและตอนตัวอย่าง
2. กดปุ่ม **"⚙️ ตั้งค่า AI"** ที่แถบเมนูด้านบน
   - เลือก Backend: `Ollama` หรือ `LM Studio`
   - กดปุ่ม **"ทดสอบการเชื่อมต่อ"** เพื่อตรวจสอบสถานะและดึงรายชื่อโมเดล
   - เลือกชื่อโมเดลที่ต้องการใช้งาน แล้วกด **"บันทึกและปิด"**
3. แปะเนื้อหาภาษาอังกฤษลงในช่องซ้าย (ต้นฉบับ)
4. (ถ้ามี) เพิ่มศัพท์เฉพาะในปุ่ม **"📚 Glossary"**
5. กดปุ่ม **"🚀 แปล / แปลต่อ"** เพื่อเริ่มแปลแบบสตรีมมิ่งทันที!

---

## การ Build แบบ Self-Contained และสร้าง Installer

ผู้ใช้ปลายทาง **ไม่จำเป็นต้องติดตั้ง .NET SDK** หาก build แบบ Self-Contained

### 1. สั่ง Publish แบบ Self-Contained
```powershell
# Windows (x64)
dotnet publish src/NovelTranslator.App/NovelTranslator.App.csproj -c Release -r win-x64 --self-contained true -o publish/win-x64

# macOS (Apple Silicon)
dotnet publish src/NovelTranslator.App/NovelTranslator.App.csproj -c Release -r osx-arm64 --self-contained true -o publish/osx-arm64

# Linux (x64)
dotnet publish src/NovelTranslator.App/NovelTranslator.App.csproj -c Release -r linux-x64 --self-contained true -o publish/linux-x64
```

### 2. สร้าง Installer ด้วย Velopack (แนะนำ)
ติดตั้ง Velopack CLI ก่อน (ทำครั้งเดียว):
```powershell
dotnet tool install -g vpk
```
จากนั้นรันสคริปต์สร้างตัวติดตั้ง:
- **Windows:**
  ```powershell
  .\installer\build-velopack.ps1 -Version 5.0.0 -Runtime win-x64
  ```
  ผลลัพธ์จะได้ไฟล์ `Setup.exe` และไฟล์อัปเดตใน `installer/Releases/win-x64/`
- **macOS / Linux:**
  ```bash
  chmod +x installer/build-velopack.sh
  ./installer/build-velopack.sh 5.0.0 osx-arm64   # หรือ osx-x64 / linux-x64
  ```

### 3. ทางเลือกสำหรับ Windows: Inno Setup
หากต้องการตัวติดตั้งแบบคลาสสิก (`NovelTranslator-Setup-5.0.0-win-x64.exe`):
1. ติดตั้ง [Inno Setup](https://jrsoftware.org/isinfo.php)
2. สั่ง `dotnet publish ... -r win-x64 --self-contained true -o publish/win-x64`
3. เปิดไฟล์ `installer/setup-inno.iss` ด้วย Inno Setup Compiler แล้วกด Compile (หรือรัน `iscc installer\setup-inno.iss`)

---

## การอัปโหลดขึ้น GitHub Releases

1. สร้าง Tag เวอร์ชันใน Git เช่น:
   ```bash
   git tag v5.0.0
   git push origin v5.0.0
   ```
2. ไปที่หน้า GitHub Repository → **Releases** → **Draft a new release**
3. เลือก Tag `v5.0.0` และตั้งชื่อ Release
4. ลากไฟล์จาก `installer/Releases/win-x64/` (เช่น `NovelTranslator-win-Setup.exe`, `RELEASES`, `.nupkg` ทั้งหมด) หรือไฟล์จาก `installer/Releases/inno/` มาอัปโหลดเป็น Assets
5. กด **Publish release**

---

## หมายเหตุสำหรับผู้ใช้เมื่อเปิดโปรแกรม (SmartScreen / Gatekeeper)

เนื่องจากไฟล์ติดตั้งยังไม่ได้ลงลายเซ็นดิจิทัล (Code Signing Certificate) ระบบปฏิบัติการอาจแสดงคำเตือนความปลอดภัย:

- **Windows (Windows SmartScreen):**
  1. หากขึ้นหน้าต่าง *"Windows protected your PC"* ให้กด **More info (ข้อมูลเพิ่มเติม)**
  2. กดปุ่ม **Run anyway (เรียกใช้ต่อไป)**
- **macOS (Gatekeeper):**
  1. หากขึ้นเตือน *"cannot be opened because the developer cannot be verified"* ให้คลิกขวาที่ตัวแอปแล้วเลือก **Open (เปิด)** หรือไปที่ **System Settings → Privacy & Security** แล้วกด **Open Anyway**
  2. หรือรันคำสั่งใน Terminal เพื่อปลด quarantine:
     ```bash
     xattr -cr /Applications/NovelTranslator.app
     ```
