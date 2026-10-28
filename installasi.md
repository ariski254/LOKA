# CRITICAL BUG FIX — LOKA SETUP WIZARD STEP 5 APPLY SETUP BLANK SCREEN

Bertindak sebagai Senior .NET WPF Engineer yang bertanggung jawab memperbaiki bug aplikasi Loka secara langsung.

## MASALAH UTAMA

Bug masih terjadi setelah perbaikan sebelumnya.

Alur untuk mereproduksi bug:

1. Buka aplikasi Loka dan jalankan setup wizard.
2. Masuk ke Database Configuration.
3. Lanjutkan sampai Step 5, yaitu Apply Setup.
4. Setelah masuk ke Step 5, seluruh area konten menjadi kosong. Tidak ada tampilan Apply Setup, informasi proses, tombol, progress, hasil, atau pesan error.

**Jangan menganggap masalah sudah selesai hanya karena build berhasil. Jangan mengulang redesign visual. Fokus utama adalah menemukan dan memperbaiki penyebab halaman Step 5 tidak dirender.**

## 1. REPRODUKSI DAN TELUSURI STEP 5

Periksa source code aktual sebelum melakukan perubahan.

Temukan implementasi yang mengendalikan:

- Database Configuration.
- Setup Wizard Step 5.
- Apply Setup.
- Wizard navigation dan step index.
- View yang ditampilkan untuk setiap step.
- ViewModel dan command yang digunakan oleh Step 5.
- Container atau `ContentControl` yang merender halaman aktif.
- DataTemplate yang menghubungkan ViewModel dengan View.
- Binding `DataContext`, dependency injection, resource dictionary, dan converter.
- Event atau command yang dijalankan ketika pengguna menekan Next atau Apply Setup.
- Operasi asynchronous yang berlangsung ketika setup diterapkan.

Gunakan pencarian kode untuk menemukan teks `Apply Setup`, `Database Configuration`, `Step 5`, `CurrentStep`, `SelectedStep`, `StepIndex`, dan implementasi navigasi yang sebenarnya digunakan proyek.

Jangan mengasumsikan nama file, class, atau property sebelum memeriksa source code.

## 2. TEMUKAN AKAR MASALAH, BUKAN MENUTUPI GEJALA

Periksa kemungkinan penyebab berikut satu per satu:

- Step index sudah berubah ke 5, tetapi tidak ada View yang dipetakan ke step tersebut.
- Kondisi `Visibility` menyembunyikan seluruh konten Step 5.
- `ContentControl.Content` bernilai null.
- DataTemplate tidak cocok dengan tipe ViewModel aktif.
- `DataContext` hilang atau diganti secara tidak sengaja.
- Navigasi berhasil dijalankan, tetapi View tujuan gagal dibuat.
- Exception pada constructor View, ViewModel, converter, atau resource membuat rendering gagal.
- Operasi Apply Setup mengganti atau mengosongkan konten sebelum proses selesai.
- Async command tidak menyelesaikan transisi dengan benar.
- Binding error atau resource XAML gagal dimuat.
- Layout memiliki ukuran nol, berada di luar area yang terlihat, atau tertutup oleh elemen lain.
- Perubahan UI sebelumnya merusak template atau struktur visual wizard.

Ini adalah daftar hipotesis untuk diverifikasi, bukan kesimpulan awal.

Tambahkan logging diagnostik pada titik yang relevan agar dapat diketahui:
- Step aktif sebelum navigasi.
- Step aktif setelah navigasi.
- Tipe ViewModel aktif.
- View atau DataTemplate yang dipilih.
- Awal dan akhir proses Apply Setup.
- Exception dan kegagalan pembuatan View.
- Status operasi dan navigasi setelah proses selesai.

Jangan menelan exception dengan `catch` kosong. Catat exception lengkap dan tampilkan pesan yang aman serta dapat dipahami pengguna.

## 3. PERBAIKI RENDERING STEP 5

Pastikan Step 5 mempunyai tampilan nyata dan selalu dapat dirender ketika step tersebut aktif.

Konten minimum Step 5:

- Judul **Apply Setup**.
- Penjelasan singkat mengenai proses yang sedang dilakukan.
- Daftar komponen atau konfigurasi yang benar-benar akan diterapkan.
- Indikator proses saat operasi sedang berjalan.
- Status tiap langkah berdasarkan hasil operasi aktual.
- Pesan sukses jika seluruh langkah wajib selesai.
- Pesan error yang menjelaskan masalah dan tindakan pemulihan jika proses gagal.
- Tombol yang sesuai dengan state proses, misalnya Retry atau Finish jika relevan.

Jangan menampilkan semua komponen sebagai sukses sebelum operasi benar-benar selesai.

Jangan membuat progress palsu atau menggunakan persentase yang tidak mencerminkan proses aktual.

Jika proses Apply Setup belum dimulai, tampilkan informasi dan aksi untuk memulainya. Jika proses sedang berjalan, tampilkan progress. Jika gagal, tampilkan error dan opsi pemulihan. Jika berhasil, tampilkan ringkasan hasil dan tombol untuk melanjutkan.

Jangan membiarkan seluruh area konten kosong pada salah satu state tersebut.

## 4. AUDIT NAVIGASI WIZARD

Periksa seluruh transisi step sebelumnya menuju Step 5.

Pastikan:

- Perpindahan ke Step 5 benar-benar menunjuk ke View yang tersedia.
- Step index, state wizard, dan ViewModel selalu konsisten.
- Navigasi tidak mengganti konten aktif menjadi null tanpa pengganti yang valid.
- Tombol Next tidak menjalankan navigasi ganda.
- Command asynchronous tidak menyebabkan race condition.
- Tombol Back tetap bekerja sesuai aturan wizard.
- Tombol Apply Setup tidak dapat memulai proses yang sama berkali-kali secara tidak sengaja.
- Jika proses gagal, pengguna tetap dapat melihat detail kegagalan dan mencoba kembali.
- Jika setup berhasil, navigasi menuju halaman utama Loka berjalan dengan benar.

Pertahankan seluruh validasi dan business logic yang sudah ada.

## 5. TAMBAHKAN FALLBACK AGAR TIDAK ADA LAYAR KOSONG

Jika View untuk step aktif tidak ditemukan atau gagal dipetakan, jangan diam-diam menampilkan area putih kosong.

Sediakan fallback error state yang menjelaskan bahwa halaman setup gagal dimuat, dilengkapi tindakan yang relevan untuk mencoba kembali atau kembali ke langkah sebelumnya.

Fallback ini bukan pengganti perbaikan akar masalah. Tujuannya agar kegagalan rendering berikutnya dapat didiagnosis tanpa membuat pengguna terjebak di layar kosong.

## 6. PERBAIKI LANGSUNG FILE SUMBER

- Edit file XAML, ViewModel, navigation service, dan service terkait sesuai hasil audit.
- Pertahankan arsitektur WPF dan MVVM yang digunakan proyek.
- Jangan membuat wizard baru untuk menggantikan wizard lama tanpa alasan teknis yang terbukti.
- Jangan menghapus fitur setup database atau mengubah konfigurasi runtime yang tidak berkaitan.
- Jangan mengganti implementasi dengan mock data.
- Jangan menyatakan sukses berdasarkan tampilan saja.
- Jangan berhenti setelah memberikan analisis atau rencana.

Setelah menemukan akar masalah, jelaskan penyebabnya secara spesifik dan implementasikan perbaikannya.

## 7. PENGUJIAN WAJIB

Lakukan pengujian berikut setelah perubahan:

1. Build solution.
2. Jalankan unit test yang tersedia.
3. Mulai setup dari awal dan navigasikan sampai Step 5.
4. Verifikasi bahwa Apply Setup benar-benar tampil.
5. Verifikasi konten ketika proses belum dimulai.
6. Verifikasi progress ketika operasi berlangsung.
7. Verifikasi hasil sukses setelah seluruh operasi wajib selesai.
8. Simulasikan atau uji kegagalan operasi dan pastikan error state tampil.
9. Uji Retry dan Back jika tersedia.
10. Pastikan tidak ada navigasi menuju halaman kosong.
11. Pastikan setup database tetap tersimpan sesuai hasil operasi.
12. Pastikan halaman utama Loka terbuka setelah setup selesai.
13. Tutup dan buka kembali aplikasi untuk memeriksa konsistensi status setup.

Build yang berhasil saja tidak membuktikan bahwa bug telah diperbaiki.

Jika pengujian GUI tidak dapat dijalankan di lingkungan yang tersedia, nyatakan `NOT TESTED`. Jangan mengklaim Step 5 telah berfungsi tanpa verifikasi.

## 8. LAPORAN AKHIR

Berikan laporan singkat berisi:

- Akar masalah yang ditemukan.
- File yang benar-benar diubah.
- Perbaikan yang diterapkan.
- Hasil build dan unit test.
- Hasil pengujian Step 5.
- Status setiap pengujian: `PASS`, `FAIL`, atau `NOT TESTED`.
- Log atau exception penting jika masih ada.
- Masalah yang belum terselesaikan.

**Prioritas mutlak: perbaiki layar kosong pada Database Configuration → Step 5: Apply Setup terlebih dahulu. Jangan mengerjakan redesign tambahan sebelum alur ini berfungsi dan terverifikasi.**