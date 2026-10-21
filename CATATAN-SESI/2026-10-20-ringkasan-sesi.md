# Ringkasan Sesi Kerja: Panamon / PLC ROHIB / Dashboard AC / Scanner AC

Tanggal: 20 Oktober 2026 (sesi panjang, berlanjut dari beberapa hari sebelumnya)
Catatan ini dibuat sebagai "memori" sesi. Riwayat lengkap ada di transkrip Claude:
`C:\Users\70N0141\.claude\projects\C--Users-70N0141-rudy-pens\775fdff4-f024-433b-833c-d83f31803265.jsonl`

---

## 1. Peta proyek

| Proyek | Lokasi | Keterangan |
|---|---|---|
| **Panamon** (ASP.NET Core) | `Panamon_Now\MonitoringSystem` | Server 10.83.33.103:6003 (IIS, PC server `29485ACP101-103`). Publish: `dotnet publish MonitoringSystem.csproj -c Release -o publish` |
| **PLC ROHIB / Plclogger** | `Panamon_Now\PLC_ROHIB\Plclogger` | Ikut ter-publish di dalam Panamon (`publish\PlcRohib`), port 5000, diakses lewat `/plcrohib-app/`. **Cukup publish Panamon.** |
| **Dashboard AC** | `Dashboard AC-20260828T030209Z-1-001\Dashboard AC` | Aplikasi terpisah (jangan digabung ke Panamon). Publish ke subfolder `publish`. |
| **Scanner AC** | `INI YANTO SCANNERNYA\...\Panasonic_ScannerAC\iniReal` | Ada perubahan orang lain yang belum di-commit; hati-hati. |

Aturan kerja yang disepakati:
- Panamon tidak dijalankan ke database asli untuk tes.
- Tulis ke database hanya dengan izin user.
- Logger PLC hanya jalan di PC server.
- Laptop **tidak bisa** menjangkau jaringan PLC 192.168.1.x; tes PLC sungguhan hanya dari PC server.

---

## 2. Yang sudah dikerjakan

### 2.1 Dashboard AC
- Expander, Evaporator, Condenser ditaruh paling atas (urutan Expander → Eva → Con), header tidak ikut ter-scroll.
- OEE Expander dihitung harian (07:00 – 07:00 hari berikutnya), sama logikanya dengan Production Data AC OEE. Operating & Quality maksimal 100%, Ability boleh di atas 100%.
- Data loss diambil langsung dari PROMOSYS (`PlcKyoshin*`), bukan dari `Dasborad_AC`, karena job sync SQL Agent ("Sync PROMOSYS to Dashboard_AC", tiap 30 detik) tidak pernah menghapus baris lama → loss "hantu".
- Break time ditandai di bar (bar biru gelap dikembalikan), load time tidak dikurangi loss.
- Total Plan fallback ke SAP Plan CU kalau user belum input.
- Klik bar loss → ke halaman `/LossTime/index`; klik Performance → halaman detail.
- Sudah di-publish beberapa kali ke folder `publish`.

### 2.2 Panamon: AC OEE / Plan Machine
- Menu **Plan Machine** langsung ke `/planmachine` (tanpa popup). Dropdown **SOURCE**: Expander paling atas, lalu Brazing (`/planbrazing`).
- 23 baris loss "Other" Expander yang reason-nya kosong dihapus dari database (atas permintaan user).

### 2.3 Panamon: Inventory (Expander Kyoshin 6.35 Actual vs Plan)
- Tabel inventory jadi setengah halaman; di kanan grafik **butterfly** "Expander Kyoshin 6.35 Actual vs Plan (Assembly)" dari `PlcKyoshinChangePlan`.
- Filter From/To (default bulan tabel, maks 62 hari), baris 0/0 disembunyikan, hover satu baris biru tipis, ikon kalender putih.
- File: `Pages/ACOEE/Inventory/*`, `wwwroot/js/inventory-runs.js`, `wwwroot/css/inventory.css`.

### 2.4 Panamon: Import SAP Plan / PSI
- Worker `SapPlanImportWorker` (tiap 3 jam, hanya di PC server) sempat tidak menulis sejak 4 Okt → dugaan app pool IIS idle (saran: AlwaysRunning, Idle Timeout 0, Preload Enabled) atau akses UNC.
- Model yang tidak ada di MasterData sekarang memakai **SUT dummy 35 detik** + keterangan (badge oranye di Preview SAP). Audit log mencatat jumlah dummy.
- Import Oktober dijalankan manual sekali: CU 34.156 dan CS 34.158 (19 Okt 11:33).

### 2.5 Scanner AC: halaman EVACOND (front-end saja)
- Halaman baru `Evacond`, `LossTimeEvaporator`, `LossTimeCondenser`. Kalau `Scanner:Line = EVACOND` (appsettings.Development.json), halaman langsung ke EVACOND. Tulisan Assembly → EVACOND, CU → Evaporator, CS → Condenser.
- **Sumber data belum ada** (database belum di-setup user).

### 2.6 Plan Brazing (Condenser = CU / MCH1-01, Evaporator = CS / MCH1-02)
- **Isi dari Rencana Database**: ambil dari SAP Plan / PSI harian (Shift NS, qty = Normal + OVT), tanpa model KIOS & DN, urutan sama dengan prioritas PSI Expander (`psi-weekly-priority.json`).
- **SUT** di Master Data Produk (tab Plan Machine):
  - Condenser: 69 model, dari PROMOSYS `MasterData` MCH1-01.
  - Evaporator: 67 model, dari PROMOSYS `MasterData` MCH1-02 (semua ketemu).
  - Model tanpa SUT = **35 detik**.
  - Halaman Expander hanya membaca SUT mesin "Expander Kyoshin 6.35" (supaya tidak tertukar dengan Condenser).
- **Kirim ke PLC Brazing** (`POST api/brazing/plc/send`): menulis 120 baris Model, SUT, Prod Plan, Date PSI (ASCII `yyyy-MM-dd`), baris kosong ikut dikosongkan, lalu baca ulang untuk verifikasi. Tidak ada mode simulasi.
- **Tombol "🔌 Cek Koneksi PLC"** (`GET api/brazing/plc/status`): buka koneksi + baca 1 register, tidak menulis apa pun. Badge hijau/merah + jam cek.
- Konfigurasi di `PlcRohib\appsettings.json`:
  ```json
  "BrazingPlc": { "Ip": "192.168.1.10", "Port": 5010 }
  ```
  **Port 5010 hanya asumsi** (disamakan dengan Expander) → perlu dicek di PLC.
- Panamon sudah di-publish dengan semua perubahan ini (20 Okt).

---

## 3. Address PLC

### 3.1 Expander Kyoshin 6.35: PLC 192.168.1.30:5010 (MC/SLMP 3E, HslCommunication `MelsecMcNet`)

**Baris Plan Production** (120 baris, 8 baris per halaman GOT), baris n mulai dari `R1000 + 20(n−1)`:

| Kolom | Offset | Baris 1 | Baris 2 |
|---|---|---|---|
| Model Name (ASCII, 10 word) | +0 | R1000 | R1020 |
| Prod. Plan | +10 | R1010 | R1030 |
| SUT | +11 | R1011 | R1031 |
| Actual | +12 | R1012 | R1032 |
| Defect | +14 | R1014 | R1034 |

**Layar utama (B-1)**: R10 model, R20 prod plan, R22 actual, R23 plan by SUT, R24 defect (dibaca blok `R10` 15 word). **R103** = timer loss berjalan (menit; 0 = tidak loss).
**Reason loss**: mulai R410, 18 reason, tiap reason 10 word (9 word nama).

**Cara menambah / mengubah address baris Expander** (harus sama di dua tempat):
1. `PLC_ROHIB/Plclogger/Program.cs` → `FactoryDataService.PlanRowMap`
   `R(rowNo, "Model", "ProdPlan", "SUT", "Actual", "Defect")`, contoh `R(1, "R1000", "R1010", "R1011", "R1012", "R1014")`.
   Kalau baris tidak ada di map, kode memakai rumus default `R1000 + 20(n−1)` (+10 plan, +11 SUT, +12 actual, +14 defect).
2. `PLC_ROHIB/Plclogger/wwwroot/js/dashboard.js` → `PLC_REGISTER_MAP`
   `{ rowNo, page, mAddr, pAddr, sAddr, actAddr, defAddr, mId, pId, sId, actId, defId, noId, noAddr }`
   (`*Id` = nomor objek di GT Designer, hanya untuk label di Live Preview).
3. Address layar utama / loss: `ReadMainScreen()` (blok R10, R103) dan konstanta `LossReasonFirstReg / LossReasonCount / LossReasonStride` di `Program.cs`.
4. Build → publish Panamon → copy ke server.

### 3.2 Brazing: PLC 192.168.1.10 (satu PLC untuk Condenser & Evaporator, register D, 120 baris per line)

Setiap kolom satu blok berurutan; baris n = offset (n−1), kolom teks 10 word per baris.

| Kolom | Condenser | Evaporator |
|---|---|---|
| Model (ASCII 10 word) | D300 + 10(n−1) | D3300 + 10(n−1) |
| SUT | D1500 + (n−1) | D4500 + (n−1) |
| Prod Plan | D1620 + (n−1) | D4620 + (n−1) |
| Actual | D1740 + (n−1) | D4740 + (n−1) |
| Plan SUT | D1860 + (n−1) | D4860 + (n−1) |
| Defect | D1980 + (n−1) | D4980 + (n−1) |
| Ket Defect (ASCII 10 word) | R2821 + 10(n−1) | R5821 + 10(n−1) |
| Date PSI (ASCII 10 word) | D2100 + 10(n−1) | D5100 + 10(n−1) |

- Home Condenser: nama mesin D200–D209, model D210–D219, plan D220, SUT D221, actual D222, plan SUT D223, defect D224.
- Home Evaporator: nama mesin D240–D249, model D250–D259, plan D260, SUT D261, actual D262, plan SUT D263, defect D264.
- Operator: Evaporator nama D280–D286 + NIK D287–D289; Condenser nama D290–D296 + NIK D297–D299.
- D275 = shift (NS 4, shift 1/2/3); D276 = hari kerja 1 / lembur 2.
- Di kode: `BrazingLines` di `Program.cs` (`BrazingLineMap(Label, ModelBase, SutBase, PlanBase, DateBase)`).

---

## 4. Yang masih tertunda

1. **Brazing PLC**: tes kirim & Cek Koneksi di PC server; pastikan port MC (5010?).
   Belum: baca Actual/Defect, Live Preview, Home/operator/shift, address loss time (user: "nanti dulu").
   Tombol "Tarik Nilai Aktual dari PLC" di halaman Brazing masih membaca PLC Expander → sebaiknya dinonaktifkan untuk Brazing.
2. **Draft Plan Brazing lama** yang SUT-nya 0 → klik ulang "Isi dari Rencana Database" sebelum kirim.
3. **Worker PSI / app pool IIS** di server (sedang dicek user).
4. **EVACOND Scanner**: sumber data loss Evaporator/Condenser (kandidat: DataMatang → AssemblyLossTime).
5. Publish ulang **Dashboard AC** bila ada perubahan baru.

---

## 5. Skrip bantu (scratchpad Claude, sementara)

`C:\Users\70N0141\AppData\Local\Temp\claude\C--Users-70N0141-rudy-pens\775fdff4-f024-433b-833c-d83f31803265\scratchpad\`
- `condseed`: isi Master Data Produk Brazing. Default Condenser; `MACHINE=Evaporator` untuk Evaporator. Dry-run dulu, `COMMIT=1` untuk simpan. Hanya menambah model yang belum ada.
- `psidry`: import PSI (dry-run / `COMMIT=1`).
- `otherq`: query baca-saja.

Folder scratchpad bisa terhapus; salin skrip ke proyek kalau mau disimpan permanen.
