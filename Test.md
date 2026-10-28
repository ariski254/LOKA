# URGENT FIX: DATABASE ERROR LOOP & PHPMYADMIN CONNECTION FAILURE — LOKA

Bertindak sebagai Senior .NET 8 WPF Engineer, Database Engineer, dan Windows Process Management Engineer.

Perbaiki langsung proyek Loka yang sudah ada di `C:\Loka`.

Dua masalah kritis masih terjadi setelah perbaikan sebelumnya:

1. Ketika menu **Databases** dibuka dari desktop Loka, jendela error muncul berulang kali sehingga mengganggu penggunaan aplikasi.
2. **phpMyAdmin masih tidak dapat diakses.** Browser gagal terhubung meskipun masalah ini sudah diminta untuk diperbaiki sebelumnya.

Jangan mengulang perbaikan sebelumnya tanpa memeriksa hasilnya. Audit implementasi aktual dan cari akar masalahnya.

## 1. HENTIKAN ERROR DIALOG YANG MUNCUL BERUNTUN

Prioritaskan masalah pada menu Databases.

Audit seluruh komponen yang berkaitan dengan halaman tersebut:
- Database View dan ViewModel.
- Constructor dan event `Loaded`.
- Data binding dan command.
- Database discovery dan connection manager.
- MySQL, MariaDB, dan PostgreSQL service manager.
- Timer, polling, event subscription, dan background task.
- Proses pengambilan status database.
- Exception handler global dan lokal.
- Navigasi dari dashboard menuju Databases.

Cari penyebab jendela error muncul berkali-kali. Jangan langsung menganggap penyebabnya hanya satu.

Periksa kemungkinan berikut:
- Event `Loaded` dipanggil berulang kali.
- Exception yang sama muncul pada setiap refresh.
- Background polling terus mengulangi operasi yang gagal.
- Beberapa komponen menampilkan dialog untuk error yang sama.
- Binding memicu operasi database berulang kali.
- Constructor atau navigasi menjalankan inisialisasi berkali-kali.
- Task asynchronous tidak ditangani dengan benar.
- Timer tetap aktif setelah halaman ditutup.
- Koneksi database otomatis dicoba meskipun service belum siap.
- Error handler menampilkan `MessageBox` setiap kali exception terjadi.

### Perbaikan wajib

1. Temukan exception pertama yang memicu rangkaian error.
2. Catat stack trace dan konteks operasi yang gagal.
3. Perbaiki akar masalah, bukan hanya menyembunyikan dialog.
4. Pastikan satu kegagalan tidak memicu rangkaian dialog tanpa akhir.
5. Jangan menggunakan `MessageBox` untuk setiap kegagalan polling atau refresh.
6. Tampilkan satu notifikasi ringkas jika diperlukan, lalu tampilkan detail melalui Activity & Service Logs.
7. Jangan mengulang operasi yang gagal tanpa batas.
8. Terapkan retry terbatas dengan jeda dan cancellation jika retry memang sesuai.
9. Pastikan background task dan timer tidak berjalan ganda.
10. Pastikan navigasi keluar dan masuk halaman tidak menggandakan event subscription.
11. Jangan menelan exception dengan `catch` kosong.
12. Jangan menganggap database berhasil terhubung hanya karena tidak ada dialog error.

Jika database belum berjalan, halaman Databases harus tetap dapat dibuka secara normal. Tampilkan status service dan koneksi yang sesuai, beserta tindakan yang tersedia.

**Kegagalan koneksi database tidak boleh membuat halaman Databases terus membuka jendela error.**

## 2. PERBAIKI MENU DATABASES AGAR TETAP DAPAT DIGUNAKAN

Audit bagaimana halaman Databases menemukan dan menampilkan database.

Pastikan:
- Halaman tidak langsung membuka koneksi tanpa memeriksa status service.
- MySQL, MariaDB, dan PostgreSQL ditangani secara terpisah.
- Setiap instance menggunakan host, port, konfigurasi, dan kredensial yang benar.
- Database yang belum aktif tidak memicu exception berulang.
- Timeout koneksi diterapkan.
- Kesalahan autentikasi dibedakan dari service yang berhenti.
- Koneksi gagal tidak memblokir UI thread.
- Tombol Refresh tidak membuat koneksi atau task duplikat.
- Navigasi kembali dan membuka halaman lagi tidak menggandakan event.
- Tidak ada kredensial default yang digunakan secara diam-diam.
- Tidak ada data dummy yang ditampilkan sebagai data database nyata.

Jika tidak ada database aktif, tampilkan kondisi kosong yang informatif. Pengguna tetap harus dapat membuka halaman Settings, Projects, dan halaman lainnya.

Jangan menjalankan query atau melakukan perubahan database hanya untuk menguji apakah halaman dapat dibuka.

## 3. DIAGNOSIS PHPMYADMIN BERDASARKAN HASIL NYATA

phpMyAdmin masih tidak bisa diakses. Jangan hanya mengubah URL, port, atau konfigurasi tanpa menguji penyebabnya.

Periksa secara berurutan:

### A. PHP dan file phpMyAdmin

- Pastikan folder phpMyAdmin benar-benar tersedia.
- Periksa file inti dan konfigurasi.
- Pastikan versi PHP kompatibel.
- Periksa `php.exe`, `php-cgi.exe` jika diperlukan, `php.ini`, dan ekstensi yang dibutuhkan.
- Pastikan konfigurasi phpMyAdmin menunjuk ke instalasi yang benar.

### B. Web server

Tentukan apakah phpMyAdmin dilayani Apache atau Nginx.

- Periksa executable dan working directory.
- Jalankan pemeriksaan konfigurasi resmi sebelum startup.
- Periksa document root, alias, virtual host, dan direktori phpMyAdmin.
- Periksa port aktual yang digunakan.
- Periksa apakah port tersebut sedang digunakan proses lain.
- Periksa apakah proses langsung berhenti setelah dijalankan.
- Ambil exit code, standard output, standard error, dan error log.

Jangan menjalankan Apache dan Nginx bersamaan pada port yang sama tanpa konfigurasi yang benar.

### C. Uji koneksi HTTP

Lakukan pengujian nyata terhadap URL dan port yang dikonfigurasi.

Bedakan hasil berikut:
- Connection refused.
- Connection timeout.
- HTTP 404.
- HTTP 403.
- HTTP 500.
- Halaman PHP ditampilkan sebagai teks.
- Halaman phpMyAdmin tampil tetapi koneksi database gagal.

Setiap hasil memiliki kemungkinan penyebab yang berbeda.

Jangan menganggap phpMyAdmin berhasil hanya karena executable web server berjalan.

### D. Perbaiki tombol phpMyAdmin

Saat pengguna menekan tombol phpMyAdmin:

1. Periksa konfigurasi server yang aktif.
2. Pastikan web server siap menerima koneksi.
3. Periksa endpoint HTTP.
4. Buka URL yang benar jika pemeriksaan berhasil.
5. Jika pemeriksaan gagal, jangan membuka browser berulang kali.
6. Tampilkan satu pesan ringkas dan detail diagnosis di Activity & Service Logs.

Jangan mengarahkan semua domain `.test` ke phpMyAdmin.

## 4. PASTIKAN MYSQL, MARIADB, DAN POSTGRESQL TIDAK SALING MENGGANGGU

Periksa startup, shutdown, port, data directory, konfigurasi, permission, dan status setiap database.

Ketentuan:
- Setiap database menggunakan data directory yang sesuai.
- MySQL dan MariaDB tidak menggunakan data directory yang sama.
- Port mengikuti konfigurasi aktual.
- Periksa konflik port sebelum startup.
- Jangan menampilkan status Running jika proses berhenti.
- Bedakan service yang berjalan dari koneksi database yang berhasil.
- Jangan menginisialisasi ulang atau menghapus data pengguna secara otomatis.
- Jangan menjalankan PostgreSQL sebagai administrator.
- Jangan mengubah permission secara luas untuk memaksa service berjalan.

Jika satu database berhenti, database lain dan UI Loka harus tetap dapat digunakan.

## 5. PERBAIKI ERROR HANDLING DAN LOGGING

Buat penanganan error yang konsisten.

Setiap error penting harus mencatat:
- Waktu kejadian.
- Komponen yang mengalami error.
- Operasi yang sedang dilakukan.
- Exception type dan pesan.
- Stack trace untuk diagnosis internal.
- Status service dan koneksi.
- Path atau port yang relevan.
- Hasil retry, jika ada.

Jangan mencatat password atau kredensial sensitif.

Bedakan error yang perlu ditampilkan langsung dari error background yang cukup dicatat pada log.

Jangan menampilkan notifikasi sukses jika operasi gagal.

Jangan menyembunyikan error dengan menonaktifkan exception handler atau menghapus semua pesan kesalahan.

## 6. PENGUJIAN WAJIB

Setelah perbaikan, lakukan pengujian berikut.

**Tes A: Database tanpa service aktif**
- Buka Loka.
- Masuk ke menu Databases.
- Pastikan halaman terbuka tanpa dialog berulang.
- Pastikan status database ditampilkan dengan benar.

**Tes B: Refresh dan navigasi**
- Tekan Refresh beberapa kali.
- Keluar dari halaman Databases.
- Buka kembali halaman tersebut.
- Pastikan tidak ada timer, event, atau dialog duplikat.

**Tes C: Database aktif**
- Jalankan satu database yang tersedia.
- Verifikasi readiness.
- Uji koneksi menggunakan konfigurasi yang benar.
- Pastikan status UI sesuai kondisi aktual.

**Tes D: phpMyAdmin**
- Verifikasi PHP.
- Verifikasi konfigurasi web server.
- Jalankan web server.
- Uji koneksi HTTP ke URL yang benar.
- Pastikan respons benar-benar berasal dari phpMyAdmin.
- Jika gagal, laporkan error aktual dan akar masalahnya.

**Tes E: Ketahanan**
- Uji port yang sedang digunakan.
- Uji executable yang tidak ditemukan.
- Uji database yang berhenti mendadak.
- Uji koneksi yang timeout.
- Pastikan tidak muncul dialog tanpa akhir.

**Tes F: Regresi**
- Build seluruh solution.
- Jalankan tes yang tersedia.
- Pastikan halaman lain tetap berfungsi.
- Pastikan project dan database pengguna tidak berubah atau hilang.

## 7. ATURAN PELAKSANAAN

- Periksa kode aktual sebelum mengubahnya.
- Perbaiki source code secara langsung.
- Jangan membuat ulang aplikasi dari nol.
- Jangan hanya memperbaiki tampilan.
- Jangan menggunakan mock untuk menggantikan pengujian nyata.
- Jangan menghapus data untuk menghilangkan error.
- Jangan menganggap build sukses berarti runtime sudah benar.
- Jangan menyatakan phpMyAdmin berhasil sebelum endpoint benar-benar diuji.
- Jangan menyatakan error dialog sudah selesai sebelum menu Databases diuji berulang kali.

## 8. LAPORAN AKHIR

Laporkan:
1. Penyebab utama error beruntun pada menu Databases.
2. Penyebab utama phpMyAdmin tidak bisa diakses.
3. File yang diubah.
4. Perbaikan yang diterapkan.
5. Hasil build.
6. Hasil tes Databases.
7. Hasil tes koneksi HTTP phpMyAdmin.
8. Tes yang gagal atau belum dapat dijalankan.
9. Masalah yang masih tersisa.

Jika ada informasi yang belum tersedia, ambil dari log, konfigurasi, dan hasil pemeriksaan aktual. Jangan mengarang penyebab atau hasil pengujian.

**MULAI DARI ERROR PERTAMA YANG MEMICU DIALOG BERUNTUN DAN KEGAGALAN KONEKSI PHPMYADMIN. PERBAIKI AKAR MASALAHNYA, LALU UJI ULANG. JANGAN BERHENTI PADA ANALISIS, PERUBAHAN UI, ATAU BUILD SAJA.**