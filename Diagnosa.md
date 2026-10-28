# LOKA CRITICAL REGRESSION FIX — PHPMyAdmin, NGINX/APACHE, TERMINAL OUTPUT, DAN STABILITAS APLIKASI

Lakukan perbaikan langsung pada project LOKA yang sedang terbuka. Beberapa perubahan sebelumnya menyebabkan regresi pada fitur yang saling berkaitan.

**Prioritas utama: pulihkan seluruh fungsi yang rusak, temukan akar penyebabnya, dan pastikan satu perbaikan tidak merusak fitur lain. Jangan menambahkan fitur baru sebelum regresi ini selesai.**

## A. Masalah yang terjadi

### 1. phpMyAdmin selalu meminta Apache

Saat pengguna membuka phpMyAdmin, muncul popup:

```text
Start Apache for phpMyAdmin

Apache web server is stopped. phpMyAdmin requires Apache to be running.
Would you like to start Apache now?

Yes / No
```

Popup ini tidak boleh muncul secara otomatis hanya karena Apache berhenti. LOKA juga mendukung Nginx sehingga akses phpMyAdmin tidak boleh dipaksakan menggunakan Apache tanpa memeriksa konfigurasi aktual.

### 2. phpMyAdmin melalui Nginx meminta pengguna berpindah ke Apache

Saat pengguna mencoba membuka phpMyAdmin menggunakan konfigurasi Nginx, muncul popup yang meminta pengguna beralih ke Apache.

Periksa alur akses phpMyAdmin, web server yang dipilih atau sedang berjalan, konfigurasi virtual host, document root, port, dan URL yang dibuka.

### 3. Terminal tidak menampilkan teks

Terminal Developer Shell sekarang mengalami regresi: output teks tidak muncul sama sekali.

Periksa proses eksekusi perintah, pengambilan stdout/stderr, pembaruan UI, binding, kontrol output, scrolling, serta perubahan XAML dan ResourceDictionary yang dilakukan sebelumnya.

Jangan menganggap masalah ini hanya disebabkan warna teks. Buktikan apakah proses perintah berjalan, output diterima, dan output benar-benar dirender.

---

## B. Perbaiki arsitektur akses phpMyAdmin

### 1. Jangan mengunci phpMyAdmin ke Apache

Periksa konfigurasi phpMyAdmin yang benar-benar tersedia dalam instalasi LOKA.

Tentukan bagaimana phpMyAdmin dapat dilayani melalui konfigurasi web server yang digunakan. Dukungan Nginx dan Apache harus ditangani berdasarkan konfigurasi yang valid, bukan berdasarkan asumsi.

Ketentuan:

- Jika Apache dipilih dan konfigurasinya valid, gunakan Apache sesuai konfigurasi tersebut.
- Jika Nginx dipilih dan konfigurasinya valid, gunakan Nginx sesuai konfigurasi tersebut.
- Jangan memaksa pengguna berpindah dari Nginx ke Apache.
- Jangan menampilkan popup yang menyatakan Apache wajib jika instalasi dapat dilayani melalui Nginx.
- Jangan menganggap Nginx mendukung konfigurasi Apache secara langsung. Buat konfigurasi yang sesuai untuk masing-masing server.
- Jangan membuat konfigurasi Nginx atau Apache yang mengarah ke direktori phpMyAdmin yang salah.
- Pastikan PHP dijalankan melalui mekanisme PHP FastCGI yang benar untuk web server yang digunakan.
- Pastikan document root, alias, PHP handler, port, dan URL konsisten.
- Jangan membuka URL jika server yang dibutuhkan belum siap.

### 2. Tangani kondisi server dengan benar

Saat pengguna membuka phpMyAdmin:

1. Baca konfigurasi web server yang digunakan untuk akses phpMyAdmin.
2. Periksa status service dan port yang relevan.
3. Validasi konfigurasi server yang akan digunakan.
4. Jika server yang dipilih berhenti, tampilkan informasi sesuai server tersebut dan minta konfirmasi sebelum menjalankannya jika memang diperlukan.
5. Jangan mengubah konfigurasi atau pilihan web server secara diam-diam.
6. Jangan menghentikan Apache atau Nginx yang sedang digunakan untuk situs lain.
7. Jangan mengaktifkan server lain hanya karena server pilihan sedang berhenti.
8. Setelah server berjalan, pastikan endpoint phpMyAdmin benar-benar dapat diakses sebelum membuka browser.

Jika konfigurasi phpMyAdmin hanya tersedia untuk salah satu web server, tampilkan keterbatasan yang sebenarnya dan jelaskan langkah yang diperlukan. Jangan menampilkan pesan generik yang memaksa perpindahan server.

### 3. Hilangkan popup yang salah

Cari seluruh dialog dan kondisi yang menghasilkan pesan:

- `Start Apache for phpMyAdmin`
- `Apache web server is stopped`
- Pesan yang menyuruh pengguna berpindah dari Nginx ke Apache.
- Dialog lain yang menggunakan asumsi bahwa phpMyAdmin selalu membutuhkan Apache.

Perbaiki kondisi pemicunya. Jangan sekadar menyembunyikan popup karena aplikasi tetap harus menangani kondisi service dan konfigurasi secara benar.

---

## C. Pulihkan terminal yang tidak menampilkan output

Lakukan investigasi menyeluruh terhadap Developer Shell.

### 1. Periksa alur output

Pastikan alur berikut benar-benar berfungsi:

`Command Input → Process Execution → stdout/stderr Capture → Output Model → UI Binding → Text Rendering`

Periksa apakah perubahan terakhir menyebabkan:

- Output tidak diterima dari proses.
- Output diterima tetapi tidak dimasukkan ke koleksi atau model tampilan.
- Binding ke kontrol teks terputus.
- Warna foreground sama dengan background.
- Kontrol output memiliki tinggi nol atau terpotong.
- TextBox, RichTextBox, atau kontrol terminal tidak memperbarui tampilannya.
- Dispatcher UI tidak dipanggil dengan benar.
- Exception saat render ditelan tanpa dicatat.
- Output buffer tidak diperbarui.
- Resource XAML yang hilang menghalangi pembuatan kontrol terminal.

Jangan menebak. Gunakan log dan hasil eksekusi untuk menemukan titik kegagalan yang sebenarnya.

### 2. Pulihkan tampilan output

- Teks hasil perintah harus terlihat jelas dengan kontras yang memadai.
- Output panjang dapat di-scroll.
- Pengguna dapat menyeleksi teks menggunakan drag.
- Klik kanan menampilkan menu konteks yang berfungsi.
- Copy melalui klik kanan dan `Ctrl+C` tetap bekerja.
- Jangan menambahkan tombol Copy Command atau Copy Output.
- Input prompt dan working directory harus tetap berfungsi.
- Jangan menghilangkan output error asli.
- Jangan membuat ulang seluruh terminal jika masalah dapat diperbaiki pada binding atau rendering yang sudah ada.

### 3. Uji eksekusi terminal

Jalankan perintah yang aman:

```powershell
Write-Output "LOKA_TERMINAL_OUTPUT_TEST"
php -v
composer -V
node -v
npm -v
git --version
mysql --version
```

Pastikan teks hasil perintah benar-benar terlihat di UI, bukan hanya tercatat di log.

Uji juga perintah yang menghasilkan error untuk memastikan output error tetap tampil.

---

## D. Periksa regresi pada perubahan sebelumnya

Jangan hanya memperbaiki dua gejala yang terlihat. Audit perubahan terakhir yang berkaitan dengan:

- phpMyAdmin launcher.
- Service manager Apache dan Nginx.
- Pengaturan web server aktif.
- Konfigurasi PHP FastCGI.
- Terminal View dan ViewModel.
- Process runner dan output capture.
- XAML StaticResource dan DynamicResource.
- ResourceDictionary.
- Event handler dan binding.
- Popup serta dialog konfirmasi.
- Pengaturan yang disimpan dan dipulihkan saat aplikasi dibuka kembali.

Periksa apakah perubahan sebelumnya mengubah perilaku fitur lain secara tidak sengaja.

Jangan membuat class atau service duplikat. Pertahankan struktur arsitektur yang sudah ada.

---

## E. Pengujian regresi wajib

Uji kombinasi berikut sesuai konfigurasi yang benar-benar tersedia:

1. Apache berjalan, Nginx berhenti.
2. Nginx berjalan, Apache berhenti.
3. Apache dan Nginx berjalan pada port yang tidak konflik.
4. Apache dan Nginx mengalami konflik port.
5. Membuka phpMyAdmin ketika web server yang diperlukan berjalan.
6. Membuka phpMyAdmin ketika web server yang diperlukan berhenti.
7. Memastikan membuka phpMyAdmin tidak menghentikan atau memulai server lain tanpa alasan yang diperlukan.
8. Terminal dapat dibuka, menjalankan perintah, menampilkan output, dan ditutup.
9. Terminal tetap berfungsi setelah berpindah halaman dan kembali.
10. Seleksi teks, scrolling, klik kanan, dan Copy tetap berfungsi.
11. Tidak ada exception XAML atau resource yang hilang.
12. Halaman lain di LOKA tetap dapat dibuka setelah perbaikan.

Jangan memaksakan seluruh kombinasi untuk lulus jika konfigurasi tersebut memang tidak didukung. Catat keterbatasannya dengan jelas.

---

## F. Cara pengerjaan

1. Periksa source code dan log sebelum mengedit.
2. Identifikasi akar penyebab masing-masing masalah.
3. Perbaiki bagian terkait dengan perubahan seminimal mungkin.
4. Build project setelah perubahan.
5. Jalankan aplikasi dan uji fitur secara nyata.
6. Ulangi pengujian setelah perubahan lain untuk mendeteksi regresi.
7. Jika ada pengujian gagal, perbaiki sebelum menyatakan pekerjaan selesai.

Jangan hanya menghasilkan laporan analisis. Terapkan perubahan langsung pada file project.

## G. Laporan akhir

Laporkan:

- Akar penyebab popup Apache.
- Akar penyebab kegagalan akses phpMyAdmin melalui Nginx.
- Akar penyebab terminal tidak menampilkan teks.
- File yang benar-benar diubah.
- Hasil build.
- Hasil pengujian setiap fitur.
- Masalah yang masih tersisa dan alasan teknisnya.

**Kriteria selesai: phpMyAdmin mengikuti konfigurasi web server yang valid, tidak memaksa perpindahan ke Apache, terminal menampilkan output dengan benar, dan perbaikan tidak merusak fitur LOKA lainnya. Jangan mengklaim berhasil sebelum menjalankan aplikasi dan memverifikasi hasilnya.**