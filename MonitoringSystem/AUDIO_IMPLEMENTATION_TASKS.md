# Task implementasi penuh Panamon BU Audio

Fork ini hanya untuk deployment BU Audio. AC adalah referensi fungsi dan desain,
bukan sumber data atau deployment yang perlu dipertahankan. Kerjakan task
berurutan. Setiap bagian tanpa data/aturan Audio yang sah harus menampilkan
**Fitur belum tersedia** dan kebutuhan datanya. Jika sumber sah ada tetapi
tanggal pilihan kosong, tampilkan **Data belum tersedia untuk periode ini**.
Jangan memakai angka nol, data contoh, atau data AC sebagai pengganti.

## 01. Inventaris dan bersihkan seluruh jalur AC

**Prompt:** Audit semua Razor Page, controller, service, job, SQL, menu,
JavaScript, ekspor, upload, dan konfigurasi. Buat matriks route/fungsi AC ke
fungsi Audio atau status belum tersedia. Ganti seluruh referensi aktif ke
`PROMOSYS`, `MachineDB`, `MCH1-*`, PLC Kyoshin, CU/CS, IP server AC, serta
teks/logo AC yang tidak berlaku. Jangan hanya menyembunyikan menu: endpoint
lama juga harus ditutup atau dimigrasikan. Verifikasi pencarian kode dan
request jaringan saat aplikasi berjalan.

**Selesai jika:** setiap endpoint aktif telah dipetakan dan tidak ada request
Audio yang mengakses database atau servis AC.

## 02. Kontrak data dan kalender produksi

**Prompt:** Profilkan semua tabel Audio; dokumentasikan arti `Actual`,
`Target`, `DailyPlan`, `SerialNumber`, `Sut`, `LossTime`, reset/koreksi counter,
shift, istirahat, dan lini aktif. Hari produksi adalah 07.00 sampai sebelum
07.00 esok hari. Delta counter pada snapshot baru masuk ke model pada
snapshot tersebut. Pilih tanggal/lini/model pembanding yang telah disetujui
pemilik proses dan simpan query verifikasi SSMS. Jangan menebak satuan atau
rumus yang belum pasti.

**Selesai jika:** kontrak data dan contoh pembanding disetujui pemilik proses.

## 03. Database, login, dan deployment mandiri

**Prompt:** Jadikan `AUDIO_PRODUCTION` database utama fork di Development dan
server. Audit migrasi EF sebelum menulis database Audio; buang SQL startup dan
hosted service khusus AC. Konfigurasikan akun/role Audio, URL, data protection,
folder upload, logging, health check, backup, dan rollback. Simpan kredensial
server di environment variable/secret manager, bukan di repo. Uji startup dan
restart tanpa jaringan AC.

**Selesai jika:** login dan aplikasi Audio berjalan hanya dengan sumber Audio.

## 04. Master lini, produk, SUT, dan shift

**Prompt:** Buat master lini `FINAL1`–`FINAL11`, nama tampilan/status aktif,
produk, SUT, kapasitas, operator, shift, kalender, dan istirahat Audio.
Sediakan input/impor tervalidasi dan laporkan model yang belum terdaftar.
Jangan menggunakan master atau jadwal AC sebagai fallback.

**Belum tersedia:** `MasterData` dan `Product` masing-masing satu baris;
`FINAL6` dan `FINAL11` kosong. Tandai bagian yang memerlukannya.

## 05. Service perhitungan snapshot FINAL

**Prompt:** Pindahkan logika awal di `Pages/Audio/ProductionAchievement` ke
service Audio. Urutkan snapshot menurut `[DateTime], ID` dalam jendela
07.00–07.00. Hitung delta `Actual` dan `Target` per lini/model. Tangani
pergantian model, reset, counter turun karena koreksi, null, duplikasi, dan
snapshot terlambat. Uji beberapa tanggal/lini/model terhadap query SSMS.

**Selesai jika:** total lini dan model dapat direkonsiliasi dan anomali diberi
status, bukan diam-diam dihitung sebagai produksi.

## 06. Production Achievement dan plan

**Prompt:** Lengkapi halaman Audio dengan filter tanggal/lini/model/shift,
actual, target, plan resmi, selisih, grafik, detail, ekspor, serta timestamp
pembaruan. Gunakan service Task 05. Pisahkan Target snapshot dari plan resmi.
Validasi hasil terhadap SSMS dan sumber produksi Audio.

**Belum tersedia:** `SapPlan` kosong dan `ProductionPlan` lokal tidak mutakhir.
Tampilkan **Fitur plan belum tersedia** sampai sumber plan rutin tersedia.
Untuk `FINAL6`/`FINAL11` kosong, tampilkan **Fitur belum tersedia** per lini.

## 07. Quality

**Prompt:** Lengkapi `Pages/Audio/Quality` dengan ingest/input defect harian,
filter lini/model/station/kategori, tren, rasio, target, dan ekspor. Penyebut
rasio harus berasal dari output Audio yang tervalidasi. Uji jumlah dengan
SSMS serta bedakan tidak ada catatan dari defect nol.

**Belum tersedia:** `DEFECTT` terakhir 15 April 2026 dan `TargetRatio` kosong.
Riwayat tetap dapat dilihat; target rasio bertanda **Fitur belum tersedia**.

## 08. Detail dan tren Loss Time

**Prompt:** Pastikan satuan dan arti `AssemblyLossTime.LossTime` sebelum
agregasi. Buat detail kejadian, input/koreksi reason, kategori, plan vs actual,
tren, filter, lampiran, dan ekspor per lini Audio. Tangani duplikasi,
kejadian tumpang tindih, dan nilai ekstrem. Bandingkan durasi dengan SSMS.

**Belum tersedia:** semua `Reason` dan `DetailedReason` pada data yang diperiksa
kosong; `LossTimeActuals` dan lampiran kosong. Bagian terkait wajib bertanda
**Fitur belum tersedia** sampai sumber dan alur input siap.

## 09. Performance, OEE, dan monitoring mesin

**Prompt:** Petakan mesin Audio serta sumber run time, downtime, cycle time,
output, defect, target, dan shift. Gunakan `MachineData` hanya untuk
`ProductGroup = 'AUDIO'`; validasi satuan dan update berulang. Definisikan rumus
KPI bersama pemilik proses, tambahkan logger/input yang kurang, dan verifikasi
beberapa mesin/hari. Jangan memakai rumus atau PLC AC tanpa padanan Audio.

**Belum tersedia:** `Efficiency` baru satu baris dan `MachineData` bercampur
AC/AUDIO. KPI tanpa sumber atau rumus sah bertanda **Fitur belum tersedia**.

## 10. Inventory dan serial unit

**Prompt:** Pastikan arti Inventory Audio: output, WIP, stok fisik, transaksi
masuk/keluar, posisi, dan serial. Audit apakah `FINAL.SerialNumber` benar-benar
catatan per unit. Bila tidak, bangun sumber transaksi/scan Audio, input,
koreksi, rekonsiliasi, filter, dan ekspor. Jangan menyebut snapshot output
sebagai stok.

**Belum tersedia:** belum ditemukan sumber transaksi unit/stok setara `OEESN`.
Inventory bertanda **Fitur belum tersedia** sampai sumber sah tersedia.

## 11. Summary, dashboard, PWK, dan fitur pendukung

**Prompt:** Bangun Summary/dashboard dari service Audio yang tervalidasi.
Migrasikan SUT, PWK, break time, laporan, upload, dan ekspor hanya dengan
aturan/sumber Audio. Semua kartu menunjukkan sumber dan waktu pembaruan.
Lepaskan setiap fallback, query, label, dan template khusus AC.

**Belum tersedia:** tabel padanan `PWK_ACTUAL`, `PlcKyoshin*`, dan beberapa
fitur mesin AC tidak ditemukan pada Audio. Tentukan padanan proses Audio atau
tampilkan **Fitur belum tersedia**.

## 12. Uji penerimaan dan rilis Audio

**Prompt:** Uji login, role, semua route/menu, filter, input/edit, grafik,
ekspor, upload, job, reset counter, model berganti, data kosong, koneksi putus,
dan restart. Rekonsiliasi angka utama per tanggal/lini/model terhadap SSMS.
Pastikan build Audio tidak memanggil AC. Buat matriks lulus/gagal beserta
bukti, URL deployment, backup/rollback, dan pemantauan pascarilis.

**Selesai jika:** seluruh fungsi yang memiliki sumber Audio lulus; fungsi
tanpa sumber terlihat jelas sebagai **Fitur belum tersedia**. Jangan menyebut
migrasi 100% selesai selama fungsi wajib masih menunggu data/proses Audio.
