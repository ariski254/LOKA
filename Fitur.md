# MASTER PROMPT — FIX LOKA PROJECT DELETION, CURRENT DATABASE SERVER, AND PHPMYADMIN CONNECTION

Kamu harus memperbaiki bug nyata pada project Loka. Lakukan audit source code terlebih dahulu, kemudian edit file yang relevan secara langsung, jalankan pengujian, dan pastikan perubahan bekerja. Jangan hanya memberikan analisis, pseudocode, atau UI mockup.

## A. BUG 1 — HAPUS PROJECT HARUS MENGHAPUS FOLDER FISIK

### Masalah

Saat project dihapus melalui aplikasi Loka, project hanya hilang dari daftar aplikasi. Folder project yang sebenarnya masih tertinggal di disk.

### Tugas

1. Telusuri seluruh alur penghapusan project:
   - Tombol atau menu Delete/Remove Project.
   - Command pada ViewModel.
   - Service pengelolaan project.
   - Penyimpanan metadata atau daftar project.
   - Implementasi penghapusan direktori.
   - Dialog konfirmasi dan notifikasi hasil operasi.

2. Identifikasi apakah implementasi sekarang hanya menghapus metadata atau item dari `ObservableCollection`, tanpa menghapus folder fisik.

3. Perbaiki alur penghapusan agar:
   - Pengguna melihat konfirmasi sebelum project dihapus.
   - Aplikasi memverifikasi path folder project yang sebenarnya.
   - Folder project fisik dihapus ketika pengguna mengonfirmasi tindakan **Delete Project and Files**.
   - Metadata project dihapus setelah penghapusan fisik berhasil.
   - Daftar project, dashboard, dan tampilan lain langsung diperbarui tanpa perlu menutup atau membuka ulang Loka.
   - Jika penghapusan gagal, tampilkan error yang jelas dan jangan menampilkan status sukses palsu.
   - Jika folder sudah tidak ada, tangani kondisi tersebut dengan aman dan sinkronkan metadata.

4. Bedakan tindakan berikut:
   - **Remove from Loka:** menghapus project dari daftar Loka saja, tanpa menghapus file fisiknya.
   - **Delete Project and Files:** menghapus project dari Loka sekaligus menghapus folder fisiknya setelah konfirmasi eksplisit.

Jika aplikasi saat ini hanya memiliki satu tombol Delete, sesuaikan label dan konfirmasinya agar pengguna mengetahui apakah file fisik akan dihapus. Jangan diam-diam mengubah operasi Remove yang sudah ada menjadi penghapusan permanen.

5. Validasi keamanan path sebelum penghapusan:
   - Jangan pernah menghapus root direktori project Loka secara keseluruhan.
   - Tolak path kosong, root drive, direktori sistem Windows, folder instalasi Loka, dan direktori di luar lokasi project yang diizinkan.
   - Cegah path traversal, path yang dimanipulasi, serta penghapusan melalui junction atau symbolic link yang dapat mengarah ke lokasi di luar folder project.
   - Gunakan canonical/normalized path dan validasi bahwa target benar-benar merupakan folder project.
   - Jangan mengikuti symbolic link secara membabi buta saat melakukan penghapusan rekursif.

6. Jika folder tidak dapat dihapus karena file sedang digunakan, tampilkan path dan penyebab kegagalan jika dapat diketahui. Jangan menghapus metadata seolah-olah seluruh proses berhasil.

### Acceptance criteria

- Menghapus project melalui Delete Project and Files benar-benar menghapus folder beserta file di dalamnya.
- Remove from Loka tetap mempertahankan folder fisik.
- Kegagalan akses tidak menghasilkan notifikasi sukses palsu.
- Daftar project langsung sinkron dengan kondisi disk.
- Penghapusan tidak dapat menjangkau folder di luar batas keamanan.
- Operasi yang gagal dapat dicoba kembali.

## B. BUG 2 — CURRENT SERVER HARUS SESUAI DENGAN DATABASE YANG BERJALAN

### Masalah

Loka mendukung MySQL dan MariaDB. Ketika hanya satu database server yang berjalan, Current Server harus otomatis mengikuti server tersebut. Ketika keduanya berjalan, pengguna harus dapat memilih server yang digunakan.

Saat ini phpMyAdmin dapat menampilkan pilihan:

- MySQL (3306), user root.
- MariaDB (3307), user root.

Namun setelah pengguna memilih MariaDB dan me-reload phpMyAdmin, muncul error:

`mysqli::real_connect(): (HY000/2002): No connection could be made because the target machine actively refused it`

Artinya, phpMyAdmin mencoba membuka koneksi ke endpoint yang tidak menerima koneksi. Penyebab pastinya harus dibuktikan melalui konfigurasi, status service, port listener, dan log. Jangan langsung menganggap password adalah penyebabnya.

### B1. Audit seluruh konfigurasi database

Periksa implementasi nyata untuk:

- MySQL service manager dan MariaDB service manager.
- Status proses dan service Windows.
- Konfigurasi port masing-masing database.
- Host, port, username, password, socket atau transport yang digunakan.
- Konfigurasi `config.inc.php` phpMyAdmin.
- Daftar `$cfg['Servers']` dan `$i` untuk setiap server.
- Variabel environment, file konfigurasi yang dihasilkan installer, dan penyimpanan pengaturan Loka.
- Mekanisme pemilihan Current Server.
- Proses reload atau restart web server.
- Mekanisme pembuatan dan pembaruan konfigurasi phpMyAdmin.
- Port aktual yang digunakan oleh proses MySQL dan MariaDB.

Jangan mengasumsikan MySQL selalu berada di port 3306 atau MariaDB selalu berada di port 3307. Nilai tersebut boleh menjadi default, tetapi konfigurasi aktual harus menjadi sumber kebenaran.

### B2. Buat satu sumber kebenaran untuk database aktif

Implementasikan mekanisme Current Server yang konsisten antara UI, service manager, konfigurasi, dan phpMyAdmin.

Current Server harus menggambarkan database yang benar-benar sedang berjalan dan dapat menerima koneksi, bukan sekadar database terakhir yang dipilih atau disimpan di pengaturan.

Terapkan aturan berikut:

**Kondisi 1: Hanya MySQL berjalan**
- Current Server otomatis menjadi MySQL.
- phpMyAdmin diarahkan ke host dan port MySQL yang aktif.
- Status UI menampilkan MySQL sebagai server aktif.

**Kondisi 2: Hanya MariaDB berjalan**
- Current Server otomatis menjadi MariaDB.
- phpMyAdmin diarahkan ke host dan port MariaDB yang aktif.
- Tidak boleh kembali menggunakan endpoint MySQL yang sedang berhenti.
- Setelah reload, konfigurasi harus tetap benar.

**Kondisi 3: MySQL dan MariaDB berjalan bersamaan**
- Sediakan pilihan Current Server MySQL atau MariaDB.
- Tampilkan status masing-masing server dan port aktualnya.
- Saat pengguna memilih salah satu server, simpan pilihan tersebut sebagai preferensi aktif.
- Perbarui konfigurasi koneksi phpMyAdmin dengan endpoint server yang dipilih.
- Pastikan pilihan tidak tertimpa oleh nilai default saat aplikasi atau phpMyAdmin di-reload.

**Kondisi 4: Tidak ada database yang berjalan**
- Current Server menampilkan status tidak tersedia atau tidak ada server aktif.
- Jangan berpura-pura bahwa MySQL atau MariaDB siap digunakan.
- Tampilkan tindakan untuk menjalankan salah satu server.

**Kondisi 5: Kedua server gagal atau salah satu berhenti**
- Deteksi perubahan status.
- Jika server yang aktif berhenti dan server lain masih berjalan serta koneksinya valid, lakukan fallback dengan memperbarui Current Server dan konfigurasi phpMyAdmin.
- Jika tidak ada server alternatif yang sehat, tampilkan status tidak tersedia.
- Jangan mengubah pilihan secara diam-diam tanpa memperbarui UI dan konfigurasi yang terkait.

### B3. Perbaiki konfigurasi phpMyAdmin

Pastikan phpMyAdmin memiliki konfigurasi server yang valid untuk MySQL dan MariaDB.

1. Periksa konfigurasi host dan port setiap server.
2. Pastikan indeks konfigurasi `$i` benar dan tidak saling menimpa.
3. Pastikan port yang digunakan phpMyAdmin sama dengan port listener database yang benar-benar berjalan.
4. Pastikan pemilihan server tidak hanya mengubah label UI, tetapi juga endpoint koneksi yang dipakai phpMyAdmin.
5. Pastikan perubahan konfigurasi disimpan secara atomik agar file tidak terbaca setengah jadi.
6. Validasi konfigurasi sebelum menerapkannya.
7. Jika perubahan memerlukan reload atau restart service, jalankan hanya jika diperlukan dan laporkan hasilnya.
8. Jangan menimpa konfigurasi pengguna yang tidak dikelola Loka tanpa mekanisme backup dan pemulihan.
9. Pertahankan kedua server dalam daftar phpMyAdmin jika keduanya memang dikonfigurasi dan tersedia, tetapi pastikan pilihan default atau Current Server mengarah ke endpoint yang sehat.
10. Jangan hardcode password atau mengubah autentikasi root menjadi metode yang tidak aman demi membuat koneksi berhasil.

Periksa juga apakah phpMyAdmin menggunakan cookie authentication, config authentication, atau metode lainnya. Sesuaikan implementasi berdasarkan konfigurasi aktual, bukan asumsi.

### B4. Jangan menggunakan port sebagai satu-satunya indikator

Port terbuka saja tidak cukup untuk menyatakan database siap.

Lakukan pemeriksaan berlapis:

1. Periksa status service atau proses.
2. Periksa listener pada host dan port yang dikonfigurasi.
3. Lakukan koneksi database menggunakan mekanisme autentikasi yang sama atau setara dengan phpMyAdmin jika memungkinkan.
4. Bedakan server berhenti, port salah, kredensial salah, koneksi ditolak, dan server belum siap setelah startup.
5. Tunggu hingga database benar-benar siap sebelum mengarahkan phpMyAdmin ke server tersebut.

Jika port dipakai oleh proses lain, jangan menganggap proses tersebut adalah MySQL atau MariaDB hanya karena portnya sesuai.

### B5. Pastikan reload tidak merusak Current Server

Bug yang harus direproduksi:

1. Jalankan MySQL dan MariaDB.
2. Pilih MariaDB sebagai Current Server.
3. Buka phpMyAdmin.
4. Pastikan koneksi berhasil.
5. Reload halaman phpMyAdmin.
6. Pastikan koneksi tetap menggunakan endpoint MariaDB yang benar.
7. Tutup dan buka kembali phpMyAdmin.
8. Pastikan Current Server tidak kembali ke MySQL secara tidak sengaja.

Ulangi pengujian ketika hanya MySQL berjalan, hanya MariaDB berjalan, dan kedua server berhenti.

Jangan mengandalkan state sementara di memori UI. Persistensikan pilihan dengan benar dan sinkronkan dengan status runtime.

## C. UI DAN STATUS HARUS SINKRON

- Tampilkan nama Current Server, status berjalan, host, dan port aktual.
- Gunakan indikator loading ketika memeriksa status atau mengganti server.
- Nonaktifkan pilihan yang tidak tersedia atau tampilkan alasan mengapa server tidak dapat dipilih.
- Setelah aksi Start, Stop, atau Select Current Server, perbarui tampilan secara langsung setelah hasil backend terverifikasi.
- Jangan meminta pengguna berpindah halaman untuk melihat status terbaru.
- Tangani error dengan pesan yang dapat dipahami dan tombol retry jika relevan.
- Jangan menampilkan status Running jika koneksi database gagal diverifikasi.
- Pastikan perubahan pada konfigurasi phpMyAdmin tidak merusak daftar server atau koneksi database lain.

## D. TESTING WAJIB

Tambahkan automated tests menggunakan mock untuk status service, port, koneksi, dan file konfigurasi bila diperlukan.

Uji sekurangnya:

1. Delete Project and Files menghapus folder fisik.
2. Remove from Loka tidak menghapus folder.
3. Path berbahaya ditolak.
4. Penghapusan gagal karena permission atau file terkunci.
5. Hanya MySQL berjalan.
6. Hanya MariaDB berjalan.
7. MySQL dan MariaDB berjalan bersamaan.
8. Current Server MariaDB tetap benar setelah reload.
9. Current Server MySQL tetap benar setelah reload.
10. Database berhenti ketika sedang dipilih.
11. Port konfigurasi berbeda dari port default.
12. Port dipakai oleh proses yang bukan database.
13. Kredensial salah dan koneksi ditolak.
14. Konfigurasi phpMyAdmin valid setelah diperbarui.
15. Perubahan konfigurasi gagal dan pemulihan berjalan dengan aman.
16. Build dan seluruh pengujian yang relevan.

Jika tersedia, lakukan integration test pada Windows dengan instalasi Loka yang sebenarnya. Jangan menganggap unit tests saja membuktikan phpMyAdmin berhasil terhubung.

## E. BATASAN

- Pertahankan arsitektur WPF/MVVM dan service yang sudah ada.
- Audit implementasi sebelum menambah kelas atau konfigurasi baru.
- Jangan membuat service manager atau mekanisme Current Server duplikat.
- Jangan merusak manajemen project, Apache/Nginx, PHP, virtual host, domain `.test`, maupun konfigurasi database yang sudah berfungsi.
- Jangan menghapus data database saat memperbaiki konfigurasi.
- Jangan menonaktifkan validasi keamanan path.
- Jangan menggunakan data palsu untuk menampilkan status Running.
- Lakukan perubahan langsung pada source code yang relevan.

## F. LAPORAN AKHIR

Berikan laporan dengan format:

1. Akar masalah setiap bug.
2. Daftar file yang diubah.
3. Ringkasan perubahan.
4. Hasil build.
5. Hasil setiap pengujian dengan PASS, FAIL, atau NOT TESTED.
6. Bukti bahwa folder project benar-benar terhapus pada operasi Delete Project and Files.
7. Bukti bahwa phpMyAdmin tetap dapat terhubung ke MariaDB setelah reload.
8. Bukti bahwa Current Server mengikuti database yang benar-benar berjalan.
9. Masalah yang masih tersisa.

Jangan menyatakan selesai hanya karena UI berubah atau build berhasil. Perbaikan harus bekerja pada filesystem dan koneksi database yang sebenarnya.