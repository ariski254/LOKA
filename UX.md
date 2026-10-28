# MASTER PROMPT — LOKA COMPLETE UI/UX MODERNIZATION & RESPONSIVE REDESIGN

## 1. TUJUAN UTAMA

Bertindak sebagai Senior UI/UX Designer, Windows Desktop Application Designer, dan Senior .NET WPF Engineer.

Lakukan perombakan menyeluruh pada tampilan aplikasi **Loka**, sebuah local web development environment untuk Windows yang akan didistribusikan kepada komunitas developer.

Targetnya adalah menciptakan aplikasi desktop yang terlihat modern, premium, profesional, minimalis, bersih, nyaman digunakan, dan responsif.

**Rombak seluruh tampilan dan interaksi UI/UX yang sudah ada, bukan hanya dashboard, warna, sidebar, atau beberapa komponen tertentu.**

Semua halaman, window, dialog, form, dropdown, input, tabel, tombol, notifikasi, status runtime, installer, dan pengaturan harus memiliki desain yang konsisten.

Lakukan perubahan langsung pada source code dan file XAML yang relevan. Jangan hanya memberikan konsep desain, screenshot, mockup, atau contoh kode terpisah.

Pertahankan seluruh fungsi aplikasi, business logic, runtime management, database, konfigurasi, serta integrasi yang sudah bekerja.

## 2. ARAH DESAIN VISUAL

Gunakan konsep:

**Modern Windows Fluent UI + Minimalist Developer Tool + Premium Light Interface.**

Karakter visual yang diinginkan:

- Tema terang sebagai tema utama.
- Bersih, modern, elegan, dan profesional.
- Dominasi warna putih dan abu-abu netral dengan satu warna aksen yang konsisten.
- Tipografi modern dengan hierarki yang jelas.
- Ruang kosong yang cukup tanpa membuat antarmuka terlalu renggang.
- Sudut membulat secara halus dan konsisten, bukan rounded berlebihan.
- Border tipis, pemisah yang lembut, dan shadow ringan jika diperlukan.
- Ikon vektor yang konsisten dan mudah dikenali.
- Animasi mikro yang halus dan tidak mengganggu.
- Status layanan yang mudah dipahami.
- Tidak menggunakan gradient berlebihan, efek glass berlebihan, neon, atau dekorasi yang tidak berguna.
- Hindari tampilan seperti template dashboard AI generik.
- Jangan membuat UI terlihat seperti aplikasi web yang dipaksakan ke dalam jendela desktop.

Hasil akhirnya harus terasa seperti produk desktop Windows modern yang benar-benar siap dipublikasikan.

## 3. BUAT DESIGN SYSTEM TERPUSAT

Sebelum merombak setiap halaman, audit desain saat ini lalu buat design system yang digunakan oleh seluruh aplikasi.

Gunakan resource terpusat untuk:

- Warna utama dan warna aksen.
- Background halaman dan panel.
- Warna teks utama, sekunder, dan disabled.
- Warna border dan separator.
- Warna status sukses, peringatan, error, dan informasi.
- Typography dan ukuran font.
- Ketebalan font.
- Spacing dan padding.
- Radius sudut.
- Ukuran ikon.
- Tinggi kontrol.
- Shadow dan elevation.
- Focus, hover, pressed, disabled, dan selected states.
- Animasi dan durasi transisi.

Gunakan `ResourceDictionary`, `ControlTemplate`, `Style`, dan komponen reusable WPF yang sesuai.

Jangan mendefinisikan ulang style serupa pada setiap halaman jika dapat menggunakan style bersama.

Pastikan seluruh tampilan tetap konsisten ketika sebuah design token diperbarui.

## 4. ROMBAK SELURUH KOMPONEN UI

Tidak boleh ada komponen lama yang tertinggal hanya karena tidak berada di halaman utama.

### A. Button

Perbarui seluruh jenis tombol:

- Primary.
- Secondary.
- Tertiary atau text button.
- Destructive.
- Icon-only.
- Split button jika memang diperlukan.
- Tombol loading.
- Tombol disabled.
- Tombol di tabel dan toolbar.
- Tombol di dialog dan installer.

Ketentuan:

- Ukuran, padding, radius, tipografi, dan ikon konsisten.
- Hover, pressed, focused, dan disabled state terlihat jelas.
- Ikon dan teks sejajar dengan rapi.
- Tombol tidak terlalu besar atau kecil.
- Tombol icon-only memiliki tooltip yang jelas.
- Tombol loading tidak dapat dipicu berulang kali ketika proses masih berjalan.
- Tombol destructive dibedakan secara tepat.
- Jangan membuat tombol dekoratif yang tidak memiliki aksi.

### B. TextBox dan Input

Modernisasi seluruh input:

- Input teks biasa.
- Input path direktori.
- Input domain.
- Input port.
- Input username dan konfigurasi database.
- Input pencarian.
- Input angka.
- Input password.
- Input dengan prefix atau suffix jika dibutuhkan.
- Input multiline.
- Input di dialog dan halaman pengaturan.

Ketentuan:

- Label berada pada posisi yang jelas.
- Placeholder tidak menggantikan label.
- Tinggi dan padding konsisten.
- Border halus dengan focus indicator yang terlihat.
- Focus state tidak hanya bergantung pada perubahan warna yang samar.
- Validasi ditampilkan dekat dengan field terkait.
- Pesan error menjelaskan cara memperbaiki input.
- Mendukung keyboard navigation, select all, copy, paste, dan shortcut standar Windows.
- Input panjang tidak menyebabkan layout rusak.
- Path yang panjang harus dapat dibaca atau disalin dengan mudah.
- Input numerik harus memiliki validasi rentang yang benar.
- Password menggunakan masking secara tepat.

### C. ComboBox dan Dropdown

**Dropdown wajib dirancang ulang secara menyeluruh.**

Jangan hanya mengubah warna kotak ComboBox, tetapi membiarkan popup pilihan tetap menggunakan tampilan default WPF.

Perbarui:

- ComboBox.
- Dropdown pilihan PHP.
- Dropdown web server.
- Dropdown database.
- Dropdown port atau konfigurasi yang memang memerlukan pilihan.
- Dropdown theme dan pengaturan.
- Dropdown menu tindakan.
- Context menu.
- Menu navigasi dan submenu.

Ketentuan:

- Closed state dan popup state menggunakan desain yang konsisten.
- Popup memiliki padding, radius, border, dan shadow yang sesuai.
- Item pilihan memiliki tinggi yang nyaman.
- Hover dan selected state terlihat jelas.
- Teks panjang dapat ditampilkan tanpa memotong informasi penting.
- Selected value tetap terbaca.
- Panah dropdown konsisten dengan desain.
- Popup tidak terpotong oleh tepi jendela atau elemen induk.
- Dropdown dengan banyak pilihan mendukung scrolling.
- Pilihan dinonaktifkan ditampilkan dengan jelas.
- Keyboard navigation, pencarian, dan pemilihan menggunakan keyboard tetap berfungsi.
- Z-order dan fokus popup ditangani dengan benar.
- Tidak ada popup yang tertinggal setelah navigasi atau penutupan dialog.

Gunakan `ControlTemplate` yang benar-benar diterapkan pada ComboBox dan seluruh item-nya. Jangan hanya memberikan style pada tampilan luarnya.

### D. Checkbox, Radio Button, dan Toggle Switch

Modernisasi seluruh kontrol pilihan:

- Checkbox.
- Radio button.
- Toggle switch.
- Selection indicator.
- Grouped options.
- Pilihan komponen installer.

Pastikan state checked, unchecked, indeterminate jika digunakan, hover, focus, disabled, dan error ditampilkan dengan jelas.

Toggle switch harus memiliki indikator visual yang konsisten dan label yang menjelaskan fungsinya.

Jangan mengubah kontrol pilihan menjadi elemen visual palsu yang tidak dapat dioperasikan.

### E. DataGrid, Table, dan List

Modernisasi seluruh tabel dan daftar runtime, proyek, database, domain, serta log.

Ketentuan:

- Header dan isi memiliki hierarki yang jelas.
- Padding baris konsisten.
- Tinggi baris nyaman dibaca.
- Separator tidak terlalu kontras.
- Hover dan selected row state terlihat jelas.
- Status runtime menggunakan label yang mudah dipahami.
- Aksi per baris ditampilkan secara rapi.
- Teks panjang dapat dibaca melalui wrapping, tooltip, atau mekanisme yang sesuai.
- Kolom tidak saling menimpa.
- Sorting, filtering, pencarian, dan pagination tetap bekerja jika tersedia.
- Empty state, loading state, dan error state memiliki tampilan yang dirancang dengan baik.
- Tabel harus tetap dapat digunakan pada jendela kecil.

Jangan menggunakan warna sebagai satu-satunya penanda status. Sertakan label atau ikon yang bermakna.

### F. Cards, Panels, dan Containers

Rombak seluruh panel dan card:

- Runtime cards.
- Project cards.
- Database cards.
- Statistik dashboard.
- Setup progress.
- Pengaturan.
- Panel diagnostik.
- Informasi error.
- Panel konfigurasi.

Hindari penggunaan card secara berlebihan. Gunakan card hanya ketika benar-benar membantu pengelompokan informasi.

Gunakan hierarchy, spacing, separator, dan alignment yang konsisten.

Jangan membungkus setiap teks atau kontrol dalam card terpisah.

### G. Sidebar dan Navigasi

Perbarui seluruh navigasi:

- Sidebar utama.
- Active navigation item.
- Hover state.
- Ikon.
- Label menu.
- Group separator.
- Submenu jika diperlukan.
- Breadcrumb jika relevan.
- Toolbar dan navigation header.

Sidebar harus ringkas, mudah dipahami, dan tidak menghabiskan ruang berlebihan.

Pastikan halaman Overview, Projects, Apache, Nginx, PHP, Databases, Domains, phpMyAdmin, Settings, dan Logs tetap dapat diakses sesuai struktur aplikasi yang sudah ada.

Jangan menambahkan menu duplikat yang tidak memiliki fungsi berbeda.

Jangan menghapus fitur hanya untuk menyederhanakan navigasi.

### H. Dialog, Modal, dan Popup

Rombak semua dialog dan window tambahan:

- Add Project.
- Edit Project.
- Add Domain.
- Runtime configuration.
- Database configuration.
- Confirmation dialog.
- Error dialog.
- Folder picker integration.
- First-run setup.
- Installer.
- Update dialog.
- Diagnostic dialog.

Ketentuan:

- Judul dan deskripsi jelas.
- Kontrol tersusun rapi.
- Tombol utama dan sekunder mudah dibedakan.
- Dialog memiliki ukuran sesuai isi.
- Tidak ada konten yang terpotong.
- Tab order benar.
- Escape dan Enter berperilaku sesuai konteks.
- Konfirmasi destructive action tetap tersedia.
- Dialog panjang memiliki scrolling yang benar.
- Fokus kembali ke kontrol sebelumnya ketika dialog ditutup jika sesuai.

Hindari dialog yang hanya berisi pesan teknis tanpa tindakan yang bisa dipahami pengguna.

### I. Toast, Notification, Tooltip, dan Status

Modernisasi:

- Toast sukses.
- Toast error.
- Notification.
- Tooltip.
- Inline validation.
- Progress notification.
- Loading indicator.
- Status runtime.
- Status instalasi.
- Empty state.
- Error state.

Notifikasi tidak boleh menutupi kontrol penting terlalu lama.

Gunakan pesan yang jelas dan singkat. Detail teknis dapat disediakan melalui log atau bagian diagnostik.

Tooltip harus membantu pengguna, bukan mengulang semua isi layar.

### J. Progress Bar dan Loading

Perbarui seluruh indikator proses:

- Runtime startup.
- Runtime shutdown.
- Download paket.
- Ekstraksi paket.
- Instalasi komponen.
- Pembuatan domain.
- Validasi konfigurasi.
- Health check.
- Pembaruan aplikasi.

Gunakan progress yang mencerminkan proses aktual. Jika persentase tidak dapat diketahui, gunakan indikator indeterminate daripada angka palsu.

Jangan membuat UI freeze ketika proses berjalan. Gunakan async operation dan pembaruan UI yang aman.

## 5. ROMBAK SELURUH HALAMAN

Terapkan desain baru pada semua halaman aplikasi.

### Overview

- Ringkasan runtime yang jelas.
- Status layanan aktual.
- Informasi proyek dan domain.
- Quick actions yang relevan.
- Informasi diagnostik penting.
- Empty state yang informatif untuk instalasi baru.

Jangan menampilkan statistik palsu atau data dummy.

### Projects

- Daftar proyek modern.
- Pencarian dan filter yang rapi.
- Informasi framework dan domain.
- Path proyek yang mudah dibaca.
- Aksi buka, edit, dan pengelolaan proyek.
- Empty state dan validasi proyek.

### Runtime Management

Terapkan desain yang konsisten pada Apache, Nginx, PHP, MySQL, MariaDB, PostgreSQL, Node.js, Git, Composer, dan runtime lain yang memang didukung.

Tampilkan versi, status aktual, path, port, dan aksi yang relevan tanpa membuat halaman terlalu padat.

### Domains

- Form domain yang jelas.
- Document root.
- Web server aktif.
- Status konfigurasi.
- Validasi nama domain.
- Aksi create, edit, dan remove yang aman.

### Databases

- Status server.
- Versi dan port.
- Informasi koneksi.
- Aksi start, stop, dan konfigurasi.
- Status konektivitas aktual.
- Pesan error yang mudah dipahami.

Jangan menampilkan kredensial sensitif secara terbuka.

### phpMyAdmin

Pertahankan integrasi yang sudah ada. Perbarui hanya bagian UI milik Loka yang memang dikelola aplikasi. Jangan merusak tampilan atau fungsi internal phpMyAdmin dengan menyuntikkan style yang tidak kompatibel.

### Settings

- Kelompokkan pengaturan berdasarkan fungsi.
- Gunakan label, deskripsi, input, dropdown, dan toggle yang konsisten.
- Perubahan pengaturan harus tersimpan secara nyata.
- Berikan feedback setelah perubahan berhasil.
- Sediakan konfirmasi ketika perubahan memerlukan restart atau memengaruhi runtime aktif.

### Logs dan Diagnostics

- Tampilkan log dalam format yang mudah dibaca.
- Bedakan level informasi, warning, dan error.
- Sediakan pencarian dan filter jika sesuai.
- Jangan membuat teks panjang melebar ke seluruh halaman.
- Sediakan copy dan akses ke file log bila memungkinkan.
- Pertahankan detail teknis yang diperlukan untuk debugging.

### Installer dan Setup Wizard

Installer harus menggunakan design system yang sama dengan aplikasi utama.

Perbarui:

- Welcome screen.
- Pemilihan direktori.
- Pemilihan runtime.
- Pilihan web server.
- Pilihan database.
- Progress instalasi.
- Status download dan ekstraksi.
- Ringkasan hasil.
- Halaman error dan retry.

Jangan membuat installer terlihat seperti aplikasi terpisah yang berasal dari produk berbeda.

## 6. RESPONSIVE LAYOUT UNTUK WPF

Jangan mengandalkan ukuran tetap untuk seluruh layout.

Audit penggunaan:

- `Grid`.
- `GridLength` dengan `Auto` dan `*`.
- `SharedSizeGroup` jika diperlukan.
- `DockPanel`.
- `WrapPanel`.
- `UniformGrid` jika sesuai.
- `ScrollViewer`.
- `MinWidth` dan `MaxWidth`.
- `MinHeight` dan `MaxHeight`.
- `HorizontalAlignment` dan `VerticalAlignment`.
- `TextWrapping`.
- `TextTrimming`.
- `GridSplitter` jika relevan.

Gunakan fixed size hanya untuk elemen yang memang memiliki ukuran intrinsik, misalnya ikon atau kontrol kecil.

### Target pengujian

Uji seluruh halaman pada ukuran jendela berikut:

- 800 × 600.
- 1024 × 768.
- 1280 × 720.
- 1366 × 768.
- 1600 × 900.
- 1920 × 1080.

Uji juga skala DPI Windows 100%, 125%, 150%, dan 200% jika lingkungan pengujian memungkinkan.

Pastikan:

- Tidak ada tombol keluar layar.
- Tidak ada teks yang bertumpuk.
- Dropdown tidak terpotong.
- Dialog tetap dapat digunakan.
- Tabel tetap dapat diakses.
- Form dapat di-scroll jika diperlukan.
- Sidebar tidak menghilangkan area konten utama.
- Tidak ada horizontal scrolling yang tidak diperlukan.
- Tampilan tidak menjadi terlalu kecil pada resolusi rendah.
- Layout tetap seimbang pada layar besar.

Jangan menyelesaikan masalah dengan memperkecil seluruh UI secara global.

## 7. ANIMASI DAN MICRO-INTERACTIONS

Tambahkan animasi ringan jika memang membantu pengalaman pengguna:

- Hover transitions.
- Press feedback.
- Expand/collapse.
- Popup opening.
- Progress transitions.
- Navigation transitions.
- Toast appearance.
- Subtle loading feedback.

Ketentuan:

- Animasi cepat, halus, dan konsisten.
- Jangan menggunakan animasi berlebihan.
- Jangan menghambat interaksi.
- Jangan menjalankan animasi berat secara terus-menerus.
- Hindari flicker, layout jumping, dan perubahan ukuran yang mengganggu.
- Hormati pengaturan reduced motion jika tersedia atau dapat diterapkan secara tepat.
- Pastikan performa tetap baik pada komputer dengan spesifikasi rendah.

## 8. AKSESIBILITAS DAN KENYAMANAN PENGGUNAAN

Pastikan seluruh komponen:

- Mendukung keyboard navigation.
- Memiliki focus indicator yang jelas.
- Memiliki tooltip untuk ikon tanpa label jika diperlukan.
- Memiliki nama aksesibel yang sesuai.
- Menggunakan kontras teks yang memadai.
- Tidak bergantung pada warna saja untuk menyampaikan informasi.
- Memiliki area interaksi yang cukup.
- Mendukung Windows scaling.
- Menggunakan urutan tab yang masuk akal.
- Tetap mudah digunakan tanpa mouse untuk tindakan umum.

Jangan menghilangkan informasi penting demi tampilan minimalis.

## 9. ARSITEKTUR IMPLEMENTASI

Lakukan audit sebelum mengubah kode.

Gunakan komponen reusable dan style terpusat. Hindari menyalin XAML yang sama ke banyak halaman.

Jika menggunakan MVVM:

- Pertahankan pemisahan View dan ViewModel.
- Jangan memindahkan business logic ke code-behind hanya demi mempermudah styling.
- Pertahankan binding dan command yang sudah ada.
- Perbaiki binding yang rusak akibat perubahan template.
- Pastikan `DataContext`, converter, trigger, dan resource tetap benar.

Untuk custom ControlTemplate:

- Pertahankan automation properties dan keyboard behavior.
- Pertahankan two-way binding yang diperlukan.
- Pastikan dropdown popup dan focus behavior bekerja.
- Pastikan disabled state benar-benar menonaktifkan interaksi.
- Hindari memory leak dari event handler dan resource.
- Jangan membuat kontrol visual yang terlihat aktif tetapi tidak menjalankan command.

Jangan menambahkan framework UI baru tanpa alasan teknis yang jelas.

## 10. LARANGAN

Jangan:

- Hanya mengganti warna dan menyebutnya redesign.
- Hanya merombak halaman Overview.
- Membiarkan dropdown default WPF sementara elemen lain sudah modern.
- Membiarkan halaman installer menggunakan desain lama.
- Menggunakan rounded corners secara berlebihan.
- Membuat setiap elemen menjadi card.
- Menambahkan emoji sebagai pengganti ikon profesional.
- Membuat animasi berat.
- Menghapus fungsi yang sulit dipertahankan.
- Mengubah business logic runtime tanpa kebutuhan.
- Mengganti data nyata dengan mock data.
- Menghapus validasi atau konfirmasi keamanan.
- Membuat tombol, menu, dan kontrol dekoratif yang tidak bekerja.
- Menutupi error dengan menyembunyikan informasi.
- Menyatakan seluruh UI selesai hanya karena aplikasi berhasil di-build.

## 11. PENGUJIAN WAJIB

Setelah implementasi:

1. Build seluruh solution.
2. Jalankan semua unit test.
3. Buka aplikasi hasil build.
4. Periksa setiap halaman dan dialog.
5. Uji seluruh dropdown dalam kondisi terbuka dan tertutup.
6. Uji seluruh input, validasi, checkbox, radio, dan toggle.
7. Uji seluruh tombol, menu, context menu, dan command.
8. Uji keyboard navigation.
9. Uji scrolling dan resizing.
10. Uji resolusi serta DPI yang tersedia.
11. Uji installer dan setup wizard.
12. Uji loading, empty state, error state, dan success state.
13. Pastikan seluruh binding MVVM bekerja.
14. Pastikan runtime, proyek, domain, database, dan konfigurasi tetap berfungsi.
15. Periksa log error dan regresi setelah redesign.

Buat checklist per halaman dan komponen. Catat setiap item sebagai `PASS`, `FAIL`, atau `NOT TESTED`.

Jangan menyatakan semua fitur lolos jika hanya sebagian yang diperiksa.

## 12. KRITERIA AKHIR

Loka dinyatakan selesai setelah:

- Seluruh halaman menggunakan design system yang konsisten.
- Semua tombol, dropdown, input, tabel, dialog, checkbox, toggle, dan popup telah diperbarui.
- Semua elemen memiliki hover, focus, pressed, disabled, loading, dan error state yang tepat sesuai kebutuhan.
- Layout responsif dan tidak terpotong pada ukuran layar yang diuji.
- Installer dan aplikasi utama memiliki identitas visual yang sama.
- Tidak ada fungsi lama yang rusak.
- Semua interaksi tetap bekerja.
- Build dan test berhasil.
- Bug yang belum dapat diverifikasi dilaporkan secara jujur.

Mulai dari audit semua View, XAML, ResourceDictionary, ControlTemplate, dan komponen yang digunakan. Kemudian implementasikan redesign secara bertahap pada seluruh aplikasi, bukan hanya satu halaman.

## 13. BUG KRITIS: HALAMAN KOSONG SETELAH KONFIGURASI DATABASE

Saat ini terdapat bug: **setelah pengguna menyelesaikan konfigurasi database pada proses setup, aplikasi menampilkan halaman kosong**.

Bug ini wajib diperiksa dan diperbaiki sebagai bagian dari redesign. Jangan sekadar menambahkan teks atau komponen agar halaman kosong terlihat terisi. Temukan penyebab sebenarnya dan pastikan alur setup berjalan sampai selesai.

### A. Audit alur setup

Telusuri seluruh alur dari halaman konfigurasi database hingga halaman berikutnya, termasuk:

- View dan XAML halaman konfigurasi database.
- ViewModel dan command untuk menyimpan konfigurasi.
- Validasi input dan hasil koneksi database.
- Proses penyimpanan konfigurasi ke file atau penyimpanan yang digunakan aplikasi.
- Proses inisialisasi runtime dan service setelah konfigurasi.
- Logika perpindahan halaman atau navigasi.
- Pengaturan `DataContext`, binding, resource, converter, dan dependency injection.
- Kondisi loading, success, failure, dan exception.
- Proses penutupan setup wizard dan pembukaan halaman utama.
- Penentuan halaman awal saat aplikasi dijalankan kembali.

Periksa apakah halaman kosong disebabkan oleh navigasi yang mengarah ke View yang tidak tersedia, `DataContext` yang hilang, exception yang tertelan, binding error, resource yang gagal dimuat, atau kondisi transisi yang tidak ditangani.

Daftar tersebut merupakan kemungkinan yang perlu diperiksa, bukan asumsi bahwa salah satunya pasti menjadi penyebab.

### B. Perbaiki transisi setelah konfigurasi database

Implementasikan alur yang sesuai dengan arsitektur Loka saat ini:

1. Pengguna menyelesaikan konfigurasi database.
2. Aplikasi memvalidasi konfigurasi dan menjalankan pemeriksaan koneksi jika memang diperlukan.
3. Aplikasi menyimpan konfigurasi hanya setelah validasi yang diwajibkan berhasil.
4. Aplikasi menampilkan indikator proses selama operasi asynchronous berlangsung.
5. Jika berhasil, aplikasi memperbarui status setup dan menavigasikan pengguna ke halaman berikutnya yang benar.
6. Jika setup selesai, tampilkan halaman utama Loka dengan navigasi, konten, dan status runtime yang sesuai.
7. Jika gagal, pertahankan halaman yang relevan dan tampilkan pesan error serta tindakan pemulihan yang jelas.

Jangan mengasumsikan bahwa database harus langsung dijalankan atau koneksi harus selalu berhasil jika desain setup saat ini tidak mensyaratkannya. Ikuti aturan bisnis dan konfigurasi yang memang diterapkan Loka.

### C. Pastikan halaman kosong tidak muncul kembali

- Pastikan setiap transisi navigasi memiliki tujuan yang valid.
- Jangan menghapus atau mengganti konten aktif sebelum halaman tujuan siap ditampilkan.
- Pastikan operasi asynchronous tidak memicu navigasi ganda atau mengakses ViewModel yang sudah tidak aktif.
- Tangani exception dan cancellation secara benar.
- Jangan menyembunyikan exception tanpa logging.
- Jangan mengandalkan restart aplikasi sebagai solusi.
- Pastikan konfigurasi yang sudah tersimpan tidak hilang ketika aplikasi berpindah halaman.
- Pastikan pengguna dapat mencoba kembali jika proses gagal.
- Jika setup belum lengkap, arahkan pengguna ke langkah setup yang relevan, bukan halaman kosong.
- Jika setup sudah selesai, jangan memaksa pengguna mengulang konfigurasi database tanpa alasan.

### D. Pengujian regresi wajib

Uji skenario berikut:

1. Setup baru dengan konfigurasi database yang valid.
2. Konfigurasi database dengan input tidak valid.
3. Database berhenti atau koneksi gagal, jika koneksi memang diwajibkan.
4. Pengguna membatalkan proses setup.
5. Proses setup mengalami exception.
6. Pengguna menyelesaikan setup lalu berpindah halaman.
7. Aplikasi ditutup dan dijalankan kembali setelah setup selesai.
8. Konfigurasi yang sudah tersimpan dimuat kembali dengan benar.
9. Navigasi menggunakan tombol Back/Next jika tersedia.
10. Perubahan ukuran jendela saat proses setup berlangsung.

Untuk setiap skenario, pastikan tidak ada halaman kosong, konten hilang, navigasi buntu, atau status setup yang tidak konsisten.

### E. Kriteria penerimaan

Bug dianggap selesai hanya jika:

- Setelah konfigurasi database berhasil, pengguna melihat halaman berikutnya yang benar.
- Tidak ada halaman kosong selama transisi setup.
- Jika terjadi kegagalan, pesan dan tindakan pemulihan ditampilkan.
- Konfigurasi tersimpan sesuai hasil operasi yang sebenarnya.
- Aplikasi dapat dijalankan kembali tanpa merusak status setup.
- Tidak ada regresi pada installer, runtime, database, atau navigasi halaman lain.

Periksa log aplikasi, exception, dan hasil pengujian sebelum menyimpulkan penyebab. Laporkan file yang diubah, akar masalah yang ditemukan, perbaikan yang dilakukan, serta hasil pengujian dengan status `PASS`, `FAIL`, atau `NOT TESTED`.

**Hasil yang diminta adalah aplikasi Loka dengan UI/UX yang benar-benar modern secara menyeluruh, bukan sekadar perubahan warna, radius, atau sidebar.**