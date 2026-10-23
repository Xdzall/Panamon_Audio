# Panamon BU Audio — pekerjaan migrasi

Prompt lengkap per task ada di [AUDIO_IMPLEMENTATION_TASKS.md](AUDIO_IMPLEMENTATION_TASKS.md).

Fork ini ditujukan untuk deployment BU Audio mandiri. Database utama adalah
`AUDIO_PRODUCTION`. Aplikasi Panamon AC hanya menjadi referensi fungsi dan
tampilan; data serta pekerjaan background AC tidak boleh menjadi sumber Audio.

## Status implementasi

| Task | Status | Kriteria selesai |
| --- | --- | --- |
| Fondasi deployment Audio | Sebagian selesai | URL, akun, konfigurasi server, dan backup deployment diuji |
| Audit kontrak data | Belum selesai | Hari produksi, reset counter, satuan loss, shift, serta sumber plan disetujui pemilik proses |
| Production Achievement | Versi awal tersedia | Delta snapshot sudah diimplementasikan; reset dan seluruh lini perlu validasi lanjutan terhadap proses sumber |
| Master produk dan plan | Belum tersedia | Master model/SUT dan plan terkini diisi rutin |
| Quality | Riwayat DEFECTT tersedia | Defect terkini, target ratio, dan rasio tervalidasi tersedia |
| Loss Time | Sebagian data tersedia | Satuan dan reason tervalidasi; actual dan lampiran siap |
| Performance | Belum tersedia | Rumus dan data KPI per mesin Audio lengkap |
| Inventory | Belum tersedia | Sumber transaksi stok/WIP/serial tersedia |
| Summary dan dashboard lengkap | Belum tersedia | Semua angka berasal dari modul Audio yang tervalidasi |
| Uji penerimaan | Belum selesai | Seluruh menu, filter, input, dan ekspor diperiksa dengan data Audio |

## Sumber data yang ditemukan pada salinan lokal

- `FINAL1`–`FINAL10` memiliki snapshot, kecuali `FINAL6`; `FINAL11` kosong.
- `MasterData` dan `Product` masing-masing hanya satu baris.
- `SapPlan`, `TargetRatio`, dan `LossTimeActuals` kosong.
- `DEFECTT` terakhir memiliki data pada 15 April 2026.
- `AssemblyLossTime` memiliki data, tetapi semua `Reason` dan `DetailedReason`
  kosong; arti/satuan `LossTime` perlu dipastikan.
- `MachineData` berisi AC dan AUDIO; setiap query Audio harus memfilter
  `ProductGroup = 'AUDIO'`.
- Belum ditemukan sumber transaksi unit atau stok setara `OEESN` AC.

## Aturan tampilan

- **Fitur belum tersedia**: sumber, kontrak, atau aturan bisnis belum siap.
- **Data belum tersedia untuk periode ini**: sumber siap tetapi tidak ada baris
  pada tanggal yang dipilih.
- Jangan mengubah ketiadaan data menjadi nol, data contoh, atau angka AC.

## Langkah berikutnya

1. Hari produksi Audio dimulai pukul 07.00; tetapkan aturan reset counter `FINAL`.
2. Implementasikan delta snapshot per lini dan model. Kenaikan pada snapshot
   baru masuk ke model pada snapshot tersebut, termasuk saat model berubah.
3. Validasi output harian terhadap tanggal, lini, dan model sampel di SSMS.
4. Lengkapi master produk, SUT, dan sumber plan Audio yang aktif.
5. Bangun modul Quality, Loss Time, Performance, Inventory, dan Summary sesuai
   matriks di atas; setiap modul wajib memiliki verifikasi data sumber.

## Menjalankan lokal

Pastikan SQL Server `localhost\\SQLEXPRESS` memiliki `AUDIO_PRODUCTION`, lalu
jalankan `dotnet run --launch-profile http`. Konfigurasi lokal ada di
`appsettings.json`; gunakan environment variable atau secret manager untuk
kredensial server. Jangan menjalankan dua instance di port 5133 bersamaan.
Nilai `Audio:ProductionDayStartHour` adalah **7**, sesuai keputusan pemilik
proses (07.00 sampai sebelum 07.00 hari berikutnya).

Contoh validasi 21 Oktober 2026 pada `FINAL1`: perhitungan delta snapshot
memberi 570 actual untuk `RF-2400DEG-K` dan 273 actual untuk
`RF-2400DLJ-K` (total 843). Target snapshot masing-masing 580 dan 294.
