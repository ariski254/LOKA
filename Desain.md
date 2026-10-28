# LOKA — MASTER FINAL FIX: WARNA UI SERAGAM, TEKS TOMBOL, DAN DATABASE TIDAK MUNCUL

Lakukan perbaikan langsung pada source code project Loka di `C:\Loka`.

Bertindak sebagai Senior C#/.NET WPF Engineer, UI/UX Engineer, dan QA Engineer. Audit implementasi yang benar-benar digunakan aplikasi, temukan akar masalah, perbaiki source code secara langsung, build, jalankan executable terbaru, dan verifikasi hasilnya.

**Jangan hanya memberikan saran, membuat mockup, mengganti kode warna tanpa memastikan hasilnya, atau menambahkan fitur baru.**

## MASALAH UTAMA

1. Warna UI tidak berubah meskipun sudah beberapa kali diperbaiki.
2. Tombol masih memiliki warna-warni yang tidak konsisten, termasuk merah, hijau, biru, dan warna lainnya.
3. Teks tombol masih gelap meskipun background tombol berwarna.
4. Style, template, resource, atau binding lama kemungkinan masih menimpa perubahan warna.
5. Bagian atau label “Database Engine” yang tidak diminta membuat menu Database tidak rapi.
6. MySQL, MariaDB, dan PostgreSQL masih belum muncul dengan benar pada menu Database.
7. Perubahan sebelumnya belum terbukti berhasil pada aplikasi yang benar-benar dijalankan.

**Prioritas mutlak: perbaiki akar masalah, bukan menambahkan elemen UI baru. Pertahankan fungsi yang sudah benar.**

---

## 1. AUDIT PROJECT SEBELUM MENGUBAH KODE

Periksa project yang sebenarnya di `C:\Loka`.

Audit:

- File project `.csproj` dan konfigurasi build.
- `App.xaml`, `StartupUri`, serta konfigurasi startup.
- Seluruh XAML yang benar-benar digunakan aplikasi.
- Resource dictionary dan urutan `MergedDictionaries`.
- Style global, style lokal, `BasedOn`, `ControlTemplate`, `DataTemplate`, dan trigger.
- Properti `Background`, `Foreground`, `BorderBrush`, dan binding warna.
- `ContentPresenter`, `ContentTemplate`, dan `TextBlock.Foreground`.
- Custom control dan template yang menetapkan warna secara langsung.
- Style yang dibuat melalui code-behind atau C#.
- Resource duplikat atau resource yang tidak digunakan.
- ViewModel, service, collection, dan binding menu Database.
- File dan perubahan sebelumnya yang menambahkan bagian “Database Engine”.
- Lokasi executable yang benar-benar sedang dijalankan.

Jangan langsung mengganti seluruh kode hex secara massal. Petakan terlebih dahulu style yang mengendalikan tombol, sidebar, background, tab, input, menu, dan halaman Database.

Jangan membuat resource dictionary baru untuk sekadar menimpa resource lama. Perbaiki sumber style yang sebenarnya dan gunakan kembali sistem resource yang sudah ada.

## 2. SERAGAMKAN WARNA SELURUH APLIKASI

Gunakan desain terang yang minimalis, profesional, dan konsisten.

| Elemen | Warna |
|---|---|
| Background utama | `#F5F7FA` |
| Sidebar dan header | `#FFFFFF` |
| Card dan permukaan | `#FFFFFF` |
| Background hover | `#F1F5F9` |
| Background selected | `#EFF6FF` |
| Border | `#E2E8F0` |
| Teks utama | `#1E293B` |
| Teks sekunder | `#64748B` |
| Tombol utama | `#2563EB` |
| Hover tombol utama | `#1D4ED8` |
| Teks tombol utama | `#FFFFFF` |
| Tombol sekunder | `#FFFFFF` |
| Border tombol sekunder | `#CBD5E1` |
| Teks tombol sekunder | `#334155` |
| Disabled background | `#F1F5F9` |
| Disabled text | `#64748B` |

Terapkan warna melalui design token atau resource terpusat yang benar-benar dipakai oleh aplikasi.

### Aturan penggunaan warna

- Gunakan biru sebagai satu-satunya warna aksen utama untuk tombol aksi biasa.
- Mayoritas tombol menggunakan background putih, border abu-abu muda, dan teks gelap.
- Gunakan biru hanya untuk tombol aksi utama yang memang perlu ditonjolkan.
- Jangan memberikan warna berbeda pada setiap tombol.
- Jangan menggunakan warna merah untuk tombol Stop, Cancel, atau Delete biasa secara otomatis.
- Jangan menggunakan warna hijau untuk tombol Start atau aksi biasa hanya untuk membedakannya.
- Jangan menggunakan aksen ungu, oranye, cyan, neon, atau gradient warna-warni pada tombol.
- Jangan membuat setiap halaman atau card mempunyai warna tombol berbeda.
- Jangan mengubah seluruh tombol menjadi biru.
- Jangan mencampurkan tema terang dengan panel atau sidebar gelap.
- Pertahankan warna status yang memang diperlukan, tetapi jangan menjadikannya warna default tombol.
- Merah hanya boleh digunakan untuk tindakan destruktif yang benar-benar berisiko jika sesuai dengan desain yang sudah ada.
- Hijau hanya boleh digunakan untuk status keberhasilan yang memang memerlukannya, bukan sebagai warna default tombol.

Tujuannya adalah UI yang tenang, rapi, konsisten, dan mudah dibaca, bukan tampilan pelangi.

## 3. PERBAIKI WARNA TEKS TOMBOL DI SELURUH APLIKASI

Perbaiki seluruh implementasi tombol yang benar-benar digunakan, termasuk:

- `Button`
- `ToggleButton`
- `RepeatButton`
- Tombol sidebar dan header.
- Tombol Dashboard, Database, Projects, Services, Settings, dan Terminal.
- Tombol dalam card, popup, dan dialog.
- Tombol Start, Stop, Restart, Open, Edit, Delete, Save, Cancel, Refresh, serta tombol lainnya.

### Aturan foreground

**Tombol utama**
- Background `#2563EB`.
- Teks `#FFFFFF`.
- Hover background `#1D4ED8`.
- Teks tetap putih pada hover dan pressed.

**Tombol sekunder**
- Background `#FFFFFF`.
- Teks `#334155`.
- Hover background `#F1F5F9`.
- Teks tetap gelap pada hover dan pressed.

**Tombol nonaktif**
- Background `#F1F5F9`.
- Teks `#64748B`.
- Teks tetap terbaca jelas.

Jangan menggunakan warna hijau atau merah sebagai gaya tombol umum. Jika terdapat tombol destruktif yang memang perlu dibedakan, pastikan penggunaan warnanya terbatas dan teksnya memiliki kontras yang baik.

### Cari penyebab teks masih gelap

Periksa apakah:

- Style tombol tidak menetapkan `Foreground`.
- Style lokal menimpa style global.
- `ControlTemplate` menetapkan warna teks secara hardcoded.
- `ContentTemplate` memiliki `TextBlock` dengan foreground sendiri.
- `ContentPresenter` tidak mengikuti `Foreground` dari tombol.
- Binding warna gagal atau menghasilkan nilai default yang salah.
- Trigger hover, pressed, focused, disabled, atau selected menimpa warna teks.
- Style `BasedOn` masih menggunakan template lama.
- Resource dictionary yang benar tidak dimuat.
- Custom control menggunakan template sendiri.
- Code-behind atau C# menetapkan warna yang bertentangan.

Perbaiki sumber masalah yang ditemukan. Jangan sekadar menambahkan `Foreground="#FFFFFF"` pada satu tombol.

Pastikan teks tombol mengikuti foreground dari style yang benar. Jika template menggunakan `TextBlock` atau `ContentTemplate`, perbaiki agar tidak menimpa warna teks yang ditentukan oleh style tombol.

Jangan memaksa semua tombol memakai teks putih. Warna teks harus sesuai dengan background masing-masing.

Pertahankan command, binding, event handler, dan logika tombol yang sudah benar.

## 4. HAPUS TAMBAHAN “DATABASE ENGINE”

Hapus judul, label, panel, section, atau kategori “Database Engine” yang ditambahkan sebelumnya dan tidak diperlukan.

Ketentuan:

- Kembalikan menu Database ke struktur yang sederhana dan mudah dipahami.
- Jangan mengganti bagian tersebut dengan section baru.
- Jangan menambahkan card ringkasan, filter, badge, panel, atau informasi dekoratif baru.
- Jangan menghapus fitur pengelolaan database yang sudah berfungsi.
- Jangan menghapus daftar database yang diperlukan.
- Periksa perubahan sebelumnya untuk menemukan file dan komponen yang menambahkan bagian tersebut.
- Hapus hanya tambahan UI yang tidak diperlukan beserta kode pendukung yang memang khusus untuk tambahan itu.

**Menu Database harus menampilkan database secara langsung melalui struktur UI yang sudah ada. Jangan menambah kategori baru untuk menyelesaikan masalah daftar database.**

## 5. PERBAIKI MYSQL, MARIADB, DAN POSTGRESQL YANG TIDAK MUNCUL

Masalah ini wajib diselesaikan dari sumber data sampai UI, bukan sekadar memperbaiki tampilan.

MySQL, MariaDB, dan PostgreSQL yang didukung Loka harus muncul sebagai entri terpisah pada menu Database, meskipun salah satu atau semuanya sedang berhenti.

### Audit penyebab sebenarnya

Periksa:

- View dan XAML menu Database.
- ViewModel yang memasok data.
- `DataContext`, `ItemsSource`, dan binding.
- Collection yang menyimpan daftar database.
- Constructor dan initialization ViewModel.
- Service discovery dan service manager.
- Pemeriksaan service Windows.
- Pemeriksaan executable dan lokasi instalasi.
- Filter yang hanya menampilkan database yang sedang berjalan.
- Logika yang hanya memuat satu engine.
- Pemilihan server aktif yang menyebabkan database lain tersembunyi.
- Cache atau collection yang tidak diperbarui.
- Exception yang menghentikan pemuatan daftar.
- Pemanggilan asynchronous dan pembaruan collection pada UI thread.
- Perbedaan nama service Windows dan nama engine.
- Apakah daftar database dibuat dari data hardcoded, hasil discovery, atau gabungan keduanya.

Temukan titik pertama tempat database seharusnya ditambahkan ke daftar tetapi justru hilang.

### Perbaiki sumber data dan binding

Pastikan:

1. MySQL mempunyai entri tersendiri.
2. MariaDB mempunyai entri tersendiri.
3. PostgreSQL mempunyai entri tersendiri.
4. Ketiganya tidak disembunyikan ketika service berhenti.
5. Ketiganya tidak hilang hanya karena executable belum ditemukan.
6. Status instalasi, executable, service, dan koneksi dipisahkan dengan benar.
7. UI menampilkan collection yang sudah diperbarui.
8. Kegagalan mendeteksi satu engine tidak menghilangkan engine lainnya.
9. Perubahan server aktif tidak menghapus atau menyembunyikan entri database lain.
10. Daftar tidak bergantung hanya pada database yang sedang dipakai proyek.

Jika engine belum terinstal, tampilkan entri yang sesuai dengan status jujur, misalnya `Belum terinstal`. Jika terinstal tetapi service berhenti, tampilkan `Terpasang · Berhenti`. Jika service aktif, tampilkan status berdasarkan pemeriksaan aktual.

Sesuaikan teks status dengan pola UI yang sudah ada. Jangan menambahkan panel atau kategori baru hanya untuk status tersebut.

Jangan membuat data dummy, service palsu, atau status aktif palsu.

### Pertahankan fungsi database

- Start dan Stop harus bekerja terhadap service yang benar.
- Jangan menghentikan database lain secara otomatis.
- Jangan mengubah konfigurasi koneksi proyek secara diam-diam.
- Jangan mengubah server aktif tanpa tindakan pengguna.
- Jangan menyembunyikan MySQL saat MariaDB aktif atau sebaliknya.
- Jangan menyamakan keberadaan executable dengan service yang sedang berjalan.
- Pertahankan fungsi pengelolaan database yang sudah berfungsi.

## 6. VERIFIKASI WARNA PADA RUNTIME

Build yang berhasil bukan bukti bahwa warna sudah berubah.

Lakukan langkah berikut:

1. Pastikan file yang diubah berasal dari project `C:\Loka`.
2. Pastikan file XAML dan resource yang diperbaiki benar-benar digunakan aplikasi.
3. Periksa urutan resource dan konflik style.
4. Pastikan tidak ada style lokal yang menimpa perubahan.
5. Build konfigurasi aplikasi yang benar.
6. Pastikan build berhasil dan executable terbaru ditemukan.
7. Tutup hanya instance Loka lama yang relevan jika perlu.
8. Jalankan executable hasil build terbaru.
9. Periksa tampilan dan warna pada aplikasi yang benar-benar berjalan.
10. Jika warna masih sama, lanjutkan investigasi sampai penyebabnya ditemukan.

Jangan menghentikan service MySQL, MariaDB, PostgreSQL, atau proses pengguna lain untuk melakukan verifikasi.

Jika memungkinkan, ambil screenshot sebelum dan sesudah perubahan dari aplikasi runtime yang sama. Jangan menggunakan mockup sebagai bukti.

## 7. UJI SELURUH HALAMAN DAN STATE TOMBOL

Periksa:

- Dashboard
- Database
- Projects
- Services
- Settings
- Terminal
- Header, sidebar, popup, dan dialog yang tersedia

Uji tombol dalam keadaan:

- Normal
- Hover
- Pressed
- Focused
- Disabled

Pastikan:

- Tombol utama biru dengan teks putih.
- Tombol sekunder putih dengan teks gelap.
- Warna tombol konsisten di semua halaman.
- Tidak ada tombol biasa berwarna merah, hijau, atau warna aksen lain tanpa alasan fungsional yang jelas.
- Tidak ada teks gelap pada tombol biru.
- Tidak ada teks yang hilang atau sulit dibaca.
- Hover dan pressed tidak mengembalikan warna lama.
- Fungsi tombol dan navigasi tetap berjalan.

Periksa juga background, sidebar, tab, input, popup, dan terminal agar seluruh aplikasi konsisten dengan tema terang.

## 8. PENGUJIAN FUNGSIONAL DAN KEAMANAN PERUBAHAN

- Build solution.
- Jalankan unit test yang relevan.
- Periksa log runtime, binding error, resource error, dan exception.
- Verifikasi ketiga entri database pada runtime.
- Uji status service jika lingkungan pengujian memungkinkan.
- Periksa `git diff` jika repository tersedia.
- Jika bukan repository Git, gunakan perbandingan file yang tersedia. Jangan menginisialisasi Git otomatis.
- Jangan menghapus validasi, binding, command, event handler, atau test assertion.
- Jangan menghapus test hanya agar build terlihat berhasil.
- Jangan menulis ulang seluruh aplikasi.
- Jangan mengubah arsitektur tanpa bukti kebutuhan.
- Jangan menambahkan fitur di luar permintaan.
- Jangan menutupi error dengan `catch` kosong.
- Jangan mengubah kode yang tidak terkait.

Lakukan perubahan minimal yang benar-benar menyelesaikan akar masalah.

## 9. LAPORAN AKHIR BERDASARKAN BUKTI

Laporkan secara konkret:

1. Penyebab warna UI tidak berubah.
2. Penyebab teks tombol tetap gelap.
3. File, style, template, resource, atau binding yang diperbaiki.
4. Perubahan yang membuat warna tombol konsisten.
5. Konfirmasi penghapusan tambahan “Database Engine”.
6. Penyebab MySQL, MariaDB, dan PostgreSQL tidak muncul.
7. File, ViewModel, service, atau binding yang diperbaiki.
8. Hasil build dan unit test.
9. Halaman dan state tombol yang benar-benar diperiksa.
10. Database yang berhasil ditampilkan beserta status aktualnya.
11. Masalah yang masih belum terselesaikan.

Jangan mengklaim pengujian visual berhasil jika aplikasi tidak berhasil dijalankan atau tampilannya belum diperiksa.

## KRITERIA SELESAI

Pekerjaan dinyatakan selesai hanya jika:

- Warna UI benar-benar berubah pada executable terbaru.
- Tema terang konsisten di seluruh halaman.
- Tombol tidak lagi berwarna-warni secara berlebihan.
- Tombol utama biru dengan teks putih dan tombol sekunder putih dengan teks gelap.
- Warna foreground benar pada normal, hover, pressed, focused, dan disabled.
- Tidak ada style lama yang menimpa warna baru.
- Tambahan “Database Engine” yang tidak diperlukan sudah dihapus.
- MySQL, MariaDB, dan PostgreSQL muncul sebagai entri terpisah sesuai dukungan aplikasi.
- Status database sesuai keadaan aktual.
- Fungsi yang sudah benar tetap berjalan.

**Mulai dengan audit perubahan sebelumnya. Temukan akar masalah, perbaiki source code secara langsung, build, jalankan executable terbaru, dan verifikasi hasilnya. Jangan menambah UI yang tidak diminta, jangan berhenti pada perubahan kode warna, dan jangan menyatakan selesai tanpa bukti runtime.**