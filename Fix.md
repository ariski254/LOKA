# MASTER PROMPT — FIX LOKA NGINX SHA-256 CHECKSUM VERIFICATION FAILURE

Kamu bertugas memperbaiki bug instalasi Loka pada laptop baru. Jangan hanya memberikan analisis atau pseudocode. Periksa source code proyek, ubah file yang benar-benar diperlukan, jalankan pengujian, dan pastikan solusi dapat digunakan pada komputer lain.

## 1. Bug yang terjadi

Ketika Loka diinstal pada laptop lain, installer menampilkan:

```text
Installation Failed

LOKA installation failed:
Downloaded package for 'Nginx' failed SHA-256 checksum verification.

Installation log saved at:
C:\Users\ridho\AppData\Local\Temp\Loka\installer.log
```

Instalasi berhenti saat memverifikasi paket Nginx.

## 2. Audit sebelum mengubah kode

Periksa implementasi installer yang sebenarnya, termasuk:

- Proses download paket Nginx dan PHP serta paket dependensi lainnya.
- URL sumber unduhan dan versi paket yang digunakan.
- Konfigurasi expected SHA-256 untuk setiap paket.
- Fungsi hashing dan validasi checksum.
- Penanganan HTTP redirect, respons error, unduhan parsial, timeout, dan retry.
- Cache unduhan, nama file, direktori temporary, dan mekanisme ekstraksi.
- Alur Online Installer dan Offline Installer.
- Log instalasi serta mekanisme pelaporan error.
- Konfigurasi yang berbeda antara komputer developer dan komputer pengguna baru.

Baca `installer.log` dari komputer yang mengalami kegagalan jika file tersebut tersedia. Jika tidak tersedia, buat mekanisme diagnostik yang cukup untuk mengidentifikasi penyebab tanpa mengharuskan pengguna mengirimkan informasi sensitif.

Jangan mengasumsikan penyebabnya hanya karena checksum kedaluwarsa. Buktikan akar masalah dari implementasi dan bukti log.

## 3. Perbaiki verifikasi checksum dengan benar

Implementasikan mekanisme yang aman dan konsisten:

1. Setiap paket memiliki metadata yang jelas, meliputi nama, versi, URL sumber, algoritma checksum, dan expected SHA-256 yang sesuai dengan artefak resmi.
2. Gunakan SHA-256 dari file yang benar-benar selesai diunduh, bukan data respons parsial atau nilai yang dihitung dari URL.
3. Pastikan stream download telah selesai dan file sudah ditutup sebelum hashing dilakukan.
4. Bandingkan hash secara benar, termasuk format hexadecimal, kapitalisasi, dan panjang nilainya.
5. Jika checksum tidak cocok, jangan ekstrak atau jalankan paket tersebut.
6. Periksa apakah respons server berupa halaman HTML error, halaman anti-bot, redirect yang salah, atau file lain yang tidak sesuai dengan paket yang diminta.
7. Tangani unduhan terputus, file kosong, timeout, HTTP error, dan checksum mismatch dengan pesan yang spesifik.
8. Tambahkan retry terbatas untuk kegagalan jaringan sementara. Hapus atau karantina file parsial sebelum mencoba ulang.
9. Pastikan cache hanya digunakan jika metadata paket dan checksum-nya cocok. Jangan menggunakan file cache yang rusak.
10. Jangan mengunduh ulang secara terus-menerus jika sumber paket menghasilkan checksum yang tidak sesuai.

## 4. Validasi sumber dan expected checksum

Periksa asal expected SHA-256 yang saat ini digunakan Loka.

- Jika checksum hardcoded sudah tidak cocok dengan artefak yang benar, perbarui checksum berdasarkan sumber tepercaya dan versi paket yang tepat.
- Jika URL mengarah ke paket yang berubah-ubah, gunakan artefak versi tetap atau sumber distribusi resmi yang menyediakan checksum tepercaya.
- Jika checksum berasal dari metadata jarak jauh, pastikan metadata tersebut memiliki mekanisme kepercayaan yang memadai dan tidak diambil secara buta dari sumber yang sama tanpa validasi.
- Jangan menghitung checksum dari file hasil unduhan yang dicurigai, lalu langsung menjadikannya expected checksum.
- Jangan menonaktifkan SHA-256 verification, menerima semua hash, atau menjadikan checksum mismatch sebagai warning yang dapat diabaikan.
- Jangan mengganti paket dengan build tidak resmi hanya agar instalasi berhasil.

Jika checksum resmi tidak dapat diverifikasi, installer harus menghentikan pemasangan paket terkait dan menjelaskan alasannya.

## 5. Perbaiki pengalaman pengguna

Saat unduhan atau validasi gagal, tampilkan pesan yang mudah dipahami, misalnya:

- Nama paket dan versi yang bermasalah.
- Tahap yang gagal: download, validasi, atau ekstraksi.
- Apakah retry masih tersedia.
- Lokasi log untuk pemeriksaan lebih lanjut.
- Tindakan aman yang dapat dilakukan pengguna.

Jangan menampilkan stack trace panjang sebagai satu-satunya pesan di UI.

Pastikan kegagalan Nginx tidak meninggalkan status instalasi seolah-olah berhasil. Bersihkan file sementara yang tidak valid dan izinkan pengguna mencoba kembali tanpa harus menghapus seluruh Loka secara manual.

## 6. Pastikan kompatibel di komputer lain

Audit semua asumsi yang hanya benar pada komputer developer:

- Absolute path ke folder pengguna.
- Path lokal atau cache yang sudah tersedia di komputer developer.
- Hak akses direktori temporary.
- Ketergantungan pada file konfigurasi lokal.
- Sertifikat TLS, proxy, redirect, dan kompatibilitas HTTPS.
- Perbedaan versi paket atau metadata antara build dan installer.
- Ketergantungan yang tidak disertakan dalam distribusi.

Gunakan direktori yang sesuai untuk pengguna Windows saat ini. Jangan mengandalkan nama pengguna atau path developer tertentu.

## 7. Pengujian wajib

Tambahkan atau perbaiki automated tests untuk:

1. Download paket valid dan checksum cocok.
2. Hash tidak cocok.
3. File hasil download kosong atau terpotong.
4. Respons HTTP error atau HTML yang menyamar sebagai paket.
5. Timeout dan koneksi terputus.
6. Retry serta pembersihan file parsial.
7. Cache valid dan cache rusak.
8. Paket dengan versi atau metadata yang berbeda.
9. Installer gagal dengan pesan yang jelas dan tidak mengekstrak file yang tidak valid.
10. Instalasi Nginx dan PHP melalui Online Installer.
11. Offline Installer jika menggunakan mekanisme validasi yang sama.
12. Pengujian ulang setelah instalasi gagal.

Gunakan fixture atau mock untuk kasus negatif. Jangan sengaja merusak paket produksi atau menonaktifkan validasi untuk membuat tes lulus.

## 8. Verifikasi hasil

Jalankan build, unit tests, integration tests, dan pemeriksaan installer yang tersedia di repository.

Jika memungkinkan, lakukan pengujian instalasi bersih pada mesin Windows lain atau VM yang tidak memiliki cache dan dependensi developer.

Pastikan Nginx benar-benar terpasang dan dapat dijalankan. Jangan menyatakan berhasil hanya karena file berhasil diunduh atau proses installer mengembalikan exit code 0.

## 9. Batasan perubahan

- Pertahankan arsitektur WPF/MVVM dan fitur Loka yang sudah ada.
- Jangan membuat installer baru dari nol jika implementasi yang ada bisa diperbaiki.
- Hindari duplikasi downloader, checksum service, konfigurasi paket, dan installer flow.
- Jangan mengubah versi paket tanpa alasan yang dapat diverifikasi.
- Jangan menghapus fitur keamanan demi kompatibilitas.
- Lakukan perubahan langsung pada file source yang relevan.

## 10. Laporan akhir

Setelah selesai, laporkan:

- Akar masalah yang terbukti.
- File yang diubah dan alasan perubahannya.
- Expected checksum dan sumber tepercaya yang digunakan, jika diperbarui.
- Hasil build dan setiap pengujian dengan status PASS, FAIL, atau NOT TESTED.
- Apakah instalasi bersih pada komputer lain benar-benar telah diuji.
- Langkah reproduksi dan verifikasi perbaikan.
- Masalah lain yang masih belum terselesaikan.

Jangan mengklaim perbaikan berhasil jika hanya build yang lulus. Prioritas utama adalah membuat installer Loka dapat mengunduh dan memverifikasi paket yang benar secara aman dan konsisten pada komputer pengguna baru.