# LOKA — DEFINITIVE UI FIX: WHITE & BLUE DESIGN SYSTEM, WPF BUTTON FOREGROUND, SETUP & UNINSTALL AUDIT

## TUJUAN UTAMA

Perbaiki langsung project Loka di `C:\Loka`.

Fokus pada konsistensi warna seluruh aplikasi, terutama:

1. Teks tombol yang masih gelap meskipun background tombol berwarna biru.
2. Style dan template WPF yang menyebabkan warna foreground tidak konsisten.
3. Penerapan palet putih dan biru pada seluruh halaman, termasuk Setup Environment dan Uninstall.
4. Konsistensi warna tombol, checkbox, dropdown, input, tabel, dialog, progress, dan notifikasi.
5. Verifikasi bahwa perubahan benar-benar digunakan oleh aplikasi yang dijalankan.

**Jangan membuat ulang aplikasi, jangan melakukan redesign massal, dan jangan sekadar menambahkan override warna pada beberapa tombol. Audit implementasi yang ada, temukan akar masalah, pertahankan bagian yang sudah benar, lalu perbaiki secara langsung.**

---

## 1. PROSEDUR WAJIB SEBELUM MENGUBAH KODE

Sebelum melakukan perubahan:

1. Periksa struktur project di `C:\Loka`.
2. Identifikasi project startup, target framework, konfigurasi build, entry point, dan executable yang benar-benar digunakan.
3. Audit `App.xaml`, resource dictionary, style global dan lokal, `ControlTemplate`, `ContentTemplate`, serta implementasi UI terkait.
4. Cari seluruh definisi tombol, termasuk yang dibuat melalui binding atau kode C#.
5. Identifikasi implementasi halaman Setup Environment dan Uninstall yang benar-benar digunakan.
6. Telusuri style, template, resource key, dan trigger yang memengaruhi foreground serta background.
7. Identifikasi akar masalah sebelum mengubah kode.
8. Pertahankan perubahan sebelumnya yang sudah benar dan hindari perubahan di luar cakupan.

Jangan mengasumsikan nama file, nama style, lokasi executable, atau struktur project sebelum memeriksanya.

Jangan membuat file baru jika masalah dapat diselesaikan secara tepat pada implementasi yang sudah ada.

## 2. DESIGN SYSTEM RESMI: WHITE + BLUE

Gunakan putih dan biru sebagai dua warna utama identitas Loka. Abu-abu digunakan untuk teks, border, dan permukaan netral. Warna semantik hanya digunakan untuk status atau tindakan yang memang memerlukannya.

### A. Surface dan background

| Elemen | Warna |
|---|---|
| Main canvas, editor, konten utama | `#FFFFFF` |
| Sidebar, terminal, panel alat | `#FAFAFA` |
| Selected row, active item | `#F0F6FF` |
| Dropdown, popup, modal | `#FFFFFF` |
| Border panel, input, tab | `#E4E4E7` |
| Divider | `#F4F4F5` |

### B. Tipografi

| Elemen | Warna |
|---|---|
| Teks utama | `#18181B` |
| Teks sekunder | `#71717A` |
| Teks redup, shortcut, placeholder | `#A1A1AA` |

Pastikan teks disabled dan placeholder tetap terbaca. Hindari opacity berlebihan yang mengurangi keterbacaan.

### C. Tombol primer

| State | Background | Foreground |
|---|---|---|
| Normal | `#2563EB` | `#FFFFFF` |
| Hover | `#1D4ED8` | `#FFFFFF` |
| Pressed | Biru lebih gelap | `#FFFFFF` |
| Focused | Sesuai state tombol | `#FFFFFF` |
| Disabled | `#F4F4F5` | `#A1A1AA` |

### D. Tombol sekunder

| State | Background | Foreground |
|---|---|---|
| Normal | `#FFFFFF` | `#18181B` |
| Hover | `#F4F4F5` | `#18181B` |
| Pressed | Abu-abu netral lebih tegas | `#18181B` |
| Focused | Sesuai state tombol | `#18181B` |
| Disabled | `#F4F4F5` | `#A1A1AA` |

Border tombol sekunder menggunakan `#E4E4E7`.

### E. Tombol destruktif

- Background normal: putih atau transparan.
- Foreground: `#DC2626`.
- Hover background: `#FEF2F2`.
- Disabled state: warna netral redup yang tetap terbaca.

Gunakan gaya destruktif untuk tindakan penghapusan atau tindakan berisiko, termasuk Uninstall. Jangan menjadikan semua tombol merah solid.

### F. Warna status semantik

| Status | Warna |
|---|---|
| Success/running | `#16A34A` |
| Warning | `#D97706` |
| Error/failed | `#DC2626` |
| Informasi/debug | `#0284C7` |

Status netral seperti Stopped karena server sengaja dihentikan tidak boleh otomatis ditampilkan sebagai error.

**Larangan:** jangan menggunakan teal sebagai warna utama atau warna dekoratif. Jangan menggunakan hijau sebagai warna tombol biasa. Jangan menambahkan warna aksen dekoratif di luar kebutuhan semantik.

---

## 3. ATURAN WAJIB FOREGROUND TOMBOL

Semua tombol harus memiliki foreground yang ditentukan berdasarkan jenis tombol, bukan mewarisi warna yang salah dari parent, template, atau style lain.

Aturan mutlak:

- Tombol dengan background biru harus memiliki teks putih `#FFFFFF`.
- Tombol sekunder berlatar putih harus memiliki teks gelap `#18181B`.
- Tombol destruktif menggunakan teks merah `#DC2626`.
- Tombol disabled menggunakan teks redup `#A1A1AA`.
- Ikon di dalam tombol mengikuti foreground tombol, kecuali ikon status yang memiliki makna semantik tersendiri.

Aturan tersebut berlaku pada state normal, hover, pressed, keyboard-focused, dan disabled.

### Audit akar masalah WPF

Periksa secara sistematis:

1. Properti `Foreground`, `Background`, dan `BorderBrush`.
2. Implicit style dan explicit style.
3. Pewarisan style dan `BasedOn`.
4. Urutan `MergedDictionaries` serta konflik resource key.
5. `ControlTemplate` dan `ContentTemplate`.
6. `ContentPresenter` dan perilaku pewarisan foreground.
7. `TextBlock` atau elemen teks di dalam tombol yang memiliki foreground hardcoded.
8. Trigger `IsMouseOver`, `IsPressed`, `IsEnabled`, `IsFocused`, dan `IsKeyboardFocused`.
9. `TemplateBinding`, `RelativeSource`, dan binding foreground lainnya.
10. Style atau properti yang diterapkan melalui code-behind C#.
11. Converter, attached property, dan custom control.
12. Resource dictionary yang dimuat secara dinamis atau hanya digunakan pada halaman tertentu.

Perhatikan bahwa `TextBlock.Foreground` yang ditentukan secara eksplisit dapat mengalahkan foreground yang diharapkan dari parent. `ContentPresenter` juga tidak menjamin semua jenis konten mengikuti foreground tombol apabila template atau elemen anak memiliki nilai tersendiri.

Jika masalah berasal dari template bersama, perbaiki template tersebut secara terpusat. Jika terdapat komponen khusus yang memerlukan pengecualian, buat pengecualian secara terarah.

Jangan hanya menambahkan `Foreground="#FFFFFF"` ke sejumlah tombol jika akar masalahnya masih terdapat pada template global.

Jangan mengganti style global secara membabi buta karena hal tersebut dapat merusak TextBox, ComboBox, menu, dan kontrol lainnya.

## 4. AUDIT SEMBILAN TOMBOL SECARA INDIVIDUAL

Periksa semua kemunculan tombol berikut, termasuk variasi huruf besar/kecil, resource key, binding, dan implementasi dinamis melalui C#.

### A. `Setup Environment`

Jika merupakan aksi utama untuk memulai setup, gunakan background biru `#2563EB` dan foreground putih `#FFFFFF`.

### B. `Continue`

Tentukan berdasarkan konteks wizard atau halaman. Gunakan biru jika merupakan aksi utama pada langkah tersebut; selain itu gunakan gaya sekunder.

### C. `Apply Setup`

Jika merupakan aksi utama untuk menerapkan konfigurasi, gunakan tombol primer biru dengan teks putih.

### D. `Open Loka Dashboard`

Tentukan apakah tombol merupakan aksi utama atau navigasi sekunder berdasarkan alur penggunaan aktual.

### E. `New`

Periksa konteks pembuatan project, koneksi, file, atau entitas lain. Jangan otomatis menjadikannya tombol primer.

### F. `Activate`

Tentukan berdasarkan fungsi dan konteks aktual. Jangan menetapkan style primer hanya berdasarkan label.

### G. `Create`

Jika merupakan aksi utama pada dialog atau form pembuatan, gunakan tombol primer. Jika tidak, gunakan gaya sekunder.

### H. `Run Query`

Jika menjalankan query merupakan aksi utama pada panel database, gunakan tombol primer biru dengan teks putih.

### I. `Save Changes`

Jika menyimpan perubahan merupakan aksi utama pada form atau dialog, gunakan tombol primer biru dengan teks putih.

### Ketentuan untuk semua tombol

- Jangan menjadikan seluruh tombol di atas sebagai tombol primer.
- Jika beberapa tombol berdampingan, gunakan maksimal satu tombol primer yang paling menonjol jika memungkinkan.
- Pertahankan fungsi, command, event handler, validasi, binding, navigasi, ukuran, margin, padding, alignment, dan posisi.
- Jika label yang sama digunakan di beberapa tempat, periksa setiap kemunculannya dan tentukan style sesuai konteks.
- Pastikan teks dan ikon konsisten pada seluruh state.
- Jangan menambahkan event handler baru hanya untuk mengganti warna jika style WPF dapat menangani kebutuhan tersebut.

---

## 5. CHECKBOX, DROPDOWN, INPUT, DAN SETUP ENVIRONMENT

Terapkan design system yang sama pada halaman setup dan kontrol terkait tanpa mengubah logika bisnis.

### Checkbox

- Unchecked: background putih, border `#E4E4E7`.
- Checked: background biru `#2563EB`, centang putih.
- Hover: perubahan border atau background lembut.
- Focused: indikator fokus jelas.
- Disabled: warna netral yang tetap terbaca.

Pastikan centang benar-benar terlihat pada template yang digunakan.

### Dropdown dan ComboBox

- Background: `#FFFFFF`.
- Foreground: `#18181B`.
- Border: `#E4E4E7`.
- Selected item: `#F0F6FF`.
- Hover item: abu-abu netral atau biru sangat muda.
- Panah dropdown dan seluruh teks tetap terlihat pada setiap state.

Periksa juga `ComboBoxItem` dan popup template, bukan hanya properti kontrol induk.

### Input dan label

- Background input: putih.
- Teks input: `#18181B`.
- Border normal: `#E4E4E7`.
- Focused border: biru.
- Label utama: `#18181B`.
- Keterangan tambahan: `#71717A`.

### Setup wizard

- Tombol utama: biru dengan teks putih.
- Back dan Cancel: netral dengan teks gelap.
- Navigasi, binding, validasi, checkbox, dan status setup harus tetap berfungsi.
- Jangan mengubah konfigurasi default atau perilaku setup yang tidak berkaitan dengan masalah visual.

---

## 6. PERBAIKI SELURUH FITUR UNINSTALL

Audit seluruh halaman, panel, dialog, dan komponen yang berkaitan dengan fitur Uninstall. Jangan hanya mengganti warna tombol Uninstall.

Semua elemen harus mengikuti design system resmi Loka.

### A. Background dan permukaan

- Background halaman: `#FFFFFF`.
- Panel sekunder dan toolbar: `#FAFAFA`.
- Baris aplikasi terpilih: `#F0F6FF`.
- Dialog dan dropdown: `#FFFFFF`.
- Border tabel, panel, dan input: `#E4E4E7`.
- Divider: `#F4F4F5`.

Pertahankan layout, ukuran, jarak, alignment, dan struktur komponen yang sudah ada.

### B. Teks dan daftar aplikasi

- Nama aplikasi: `#18181B`.
- Versi, publisher, lokasi, dan informasi tambahan: `#71717A`.
- Shortcut dan teks redup: `#A1A1AA`.
- Baris terpilih harus tetap terbaca dengan background `#F0F6FF`.
- Ikon aplikasi tidak boleh kehilangan kontras.

Audit `ListView`, `DataGrid`, `ItemsControl`, baris aplikasi, checkbox seleksi, menu konteks, dan template item yang digunakan.

Pastikan state normal, hover, selected, focused, dan disabled konsisten. Jangan sampai teks menjadi putih atau tidak terbaca ketika baris dipilih.

### C. Tombol Uninstall dan tombol pendukung

**Uninstall**

- Gunakan gaya destruktif dengan background putih atau transparan dan teks merah `#DC2626`.
- Hover menggunakan `#FEF2F2`.
- Jika konteks desain yang sudah ada membutuhkan penekanan berbeda, pertahankan pendekatan destruktif yang konsisten dan jelas.

**Tombol pendukung**

- Refresh, Search, Filter, Back, dan Cancel: gaya sekunder dengan teks `#18181B`, background putih, dan border `#E4E4E7`.
- Aksi utama lain yang bukan destruktif: biru `#2563EB` dengan teks putih `#FFFFFF`.
- Disabled: background `#F4F4F5` dengan teks `#A1A1AA`.

Jangan mengubah semua tombol menjadi biru atau merah. Ikon mengikuti foreground tombol, kecuali ikon status yang memiliki makna semantik.

### D. Dialog konfirmasi Uninstall

- Background dialog: `#FFFFFF`.
- Judul dan teks utama: `#18181B`.
- Keterangan tambahan: `#71717A`.
- Border dan divider: `#E4E4E7`.
- Tombol Cancel: gaya sekunder dengan teks gelap.
- Tombol konfirmasi Uninstall: gaya destruktif dengan teks merah.

Pastikan nama aplikasi yang akan dihapus terlihat jelas. Jangan mengubah validasi, mekanisme keselamatan, isi konfirmasi, atau alur penghapusan yang sudah benar.

### E. Progress dan notifikasi

Gunakan warna sesuai makna:

- Proses berlangsung: biru `#2563EB`.
- Berhasil: hijau `#16A34A`.
- Peringatan: amber `#D97706`.
- Gagal: merah `#DC2626`.
- Informasi/debug: cyan `#0284C7`.
- Status netral: abu-abu.

Progress bar harus memiliki track netral dan indikator biru. Jangan menambahkan gradient atau dekorasi warna baru.

Pastikan status proses, berhasil, gagal, dan dibatalkan tidak tertukar.

### F. Audit akar masalah WPF pada Uninstall

Periksa implementasi aktual, termasuk:

- Resource dictionary dan style yang digunakan.
- `ControlTemplate` dan `DataTemplate`.
- `ListViewItem`, `DataGridRow`, dan `DataGridCell` jika digunakan.
- `ContentPresenter` dan foreground elemen teks.
- Trigger `IsMouseOver`, `IsSelected`, `IsFocused`, `IsEnabled`, dan `IsPressed`.
- Binding warna, converter, serta style yang diterapkan melalui C#.
- Dialog khusus dan custom control.
- Konflik style lokal dengan style global.

Perbaiki sumber masalahnya. Jangan membuat style baru yang tidak digunakan dan jangan menambahkan override warna secara acak.

### G. Pertahankan fungsi Uninstall

Perubahan ini hanya menyangkut tampilan dan konsistensi warna.

Jangan merusak:

- Deteksi aplikasi yang terpasang.
- Pencarian dan filter aplikasi.
- Pemilihan aplikasi.
- Informasi versi, publisher, dan lokasi.
- Dialog konfirmasi.
- Proses uninstall sebenarnya.
- Penanganan kegagalan dan pembatalan.
- Refresh daftar aplikasi setelah uninstall.
- Logging, notifikasi, dan error handling.

Setelah proses uninstall, perbarui daftar berdasarkan hasil aktual. Jangan sekadar menghilangkan baris dari tampilan apabila aplikasi masih terpasang. Jika proses gagal, jangan menampilkan seolah-olah aplikasi berhasil dihapus.

---

## 7. CENTRALIZED RESOURCE DAN STYLE YANG BENAR-BENAR AKTIF

Jika sesuai dengan arsitektur project, pusatkan warna dan style dalam resource yang sudah digunakan aplikasi.

Nama resource konseptual yang dapat digunakan:

- `ColorPrimary`
- `ColorPrimaryHover`
- `ColorPrimaryPressed`
- `ColorSurface`
- `ColorSurfaceSecondary`
- `ColorSelected`
- `ColorBorder`
- `ColorDivider`
- `ColorTextPrimary`
- `ColorTextSecondary`
- `ColorTextMuted`
- `ColorSuccess`
- `ColorWarning`
- `ColorError`
- `ColorInfo`

Nama tersebut bukan kewajiban. Gunakan konvensi yang sudah ada jika project memiliki sistem resource yang konsisten.

Ketentuan:

1. Jangan membuat resource dictionary baru tanpa kebutuhan yang jelas.
2. Jangan membuat style yang tidak pernah dimuat atau digunakan.
3. Jangan menduplikasi resource dengan key yang sama secara sembarangan.
4. Jangan menimpa seluruh style kontrol WPF tanpa memeriksa dampaknya.
5. Pertahankan kompatibilitas dengan custom control dan style yang sudah benar.
6. Pastikan resource benar-benar direferensikan oleh aplikasi yang dijalankan.
7. Hindari warna hardcoded yang bertentangan dengan design system pada komponen yang sedang diperbaiki.
8. Jangan mengubah warna elemen yang tidak terkait jika warnanya sudah benar.
9. Pastikan halaman utama, Setup Environment, Uninstall, dan dialog terkait menggunakan aturan warna yang sama.

---

## 8. BATASAN PERUBAHAN

Jangan melakukan redesign massal.

Dilarang tanpa alasan teknis yang terbukti:

- Mengubah layout seluruh aplikasi.
- Mengubah ukuran atau posisi kontrol.
- Mengganti seluruh tombol menjadi biru.
- Mengganti seluruh teks tombol menjadi putih.
- Menambahkan gradient, glassmorphism, shadow dekoratif, atau kartu baru.
- Menambahkan warna aksen dekoratif, terutama teal.
- Mengubah fungsi tombol, command, event handler, binding, validasi, atau alur setup.
- Menghapus fitur, file, konfigurasi, atau komponen yang tidak terkait.
- Mengganti framework atau arsitektur aplikasi.
- Menyembunyikan exception atau mengubah kegagalan menjadi hasil kosong.
- Mengklaim perbaikan berhasil hanya berdasarkan inspeksi source code.

Utamakan perubahan terkecil yang menyelesaikan akar masalah secara menyeluruh.

---

## 9. BUILD DAN PENGUJIAN WAJIB

Setelah perubahan selesai, lakukan verifikasi nyata.

### A. Build

1. Identifikasi project startup dan konfigurasi build yang benar.
2. Jalankan build menggunakan SDK dan toolchain yang tersedia.
3. Perbaiki error kompilasi yang disebabkan perubahan.
4. Catat warning baru yang relevan.
5. Pastikan build menggunakan source terbaru.
6. Identifikasi path executable hasil build berdasarkan konfigurasi aktual.

Jangan mengasumsikan path executable, target framework, atau nama project sebelum memeriksanya.

### B. Runtime dan pengujian visual

1. Jalankan executable hasil build terbaru.
2. Pastikan aplikasi benar-benar terbuka.
3. Periksa sembilan tombol yang ditentukan pada konteks aktualnya.
4. Periksa halaman utama, Setup Environment, Uninstall, serta dialog terkait.
5. Periksa background, foreground, border, ikon, dan keterbacaan.
6. Uji normal, hover, pressed, keyboard focus, dan disabled jika state tersebut tersedia.
7. Periksa tombol biru lain di seluruh halaman untuk menemukan teks yang masih gelap.
8. Periksa tombol sekunder yang tidak sengaja menggunakan teks putih.
9. Periksa checkbox, dropdown, input, tabel aplikasi, dialog konfirmasi, progress, dan notifikasi.
10. Periksa binding error, exception, dan regresi fungsi.
11. Pastikan tidak ada instance aplikasi lama yang menyebabkan hasil pengujian keliru.

Jika UI automation tersedia, gunakan untuk memeriksa state yang relevan. Jika tidak tersedia, lakukan pemeriksaan runtime yang dapat dilakukan dan jelaskan keterbatasannya.

Jangan menyatakan hover, pressed, focused, atau disabled berhasil diuji apabila state tersebut belum benar-benar diperiksa.

### C. Pengujian fungsi Uninstall

1. Pastikan daftar aplikasi dapat dimuat dan dicari.
2. Pastikan pemilihan aplikasi tetap berfungsi.
3. Pastikan dialog konfirmasi menampilkan aplikasi yang tepat.
4. Jangan melakukan uninstall aplikasi pengguna tanpa persetujuan eksplisit.
5. Jika pengujian penghapusan aktual tidak aman atau tidak dapat dilakukan, uji alur yang aman dan nyatakan keterbatasannya.
6. Pastikan hasil gagal tidak dilaporkan sebagai keberhasilan.
7. Pastikan daftar diperbarui sesuai hasil proses aktual.

### D. Pemeriksaan regresi

Pastikan:

- Semua tombol menjalankan fungsi semula.
- Binding dan navigasi tidak rusak.
- Setup tetap berfungsi.
- Uninstall tetap berfungsi.
- Tidak ada konflik resource baru.
- Tidak ada perubahan visual yang tidak diminta.
- Tampilan tetap konsisten setelah aplikasi ditutup dan dibuka kembali.

---

## 10. LAPORAN HASIL AKHIR

Setelah menyelesaikan implementasi dan pengujian, berikan laporan singkat dan faktual yang mencakup:

1. Akar penyebab foreground tombol yang salah.
2. File yang benar-benar diubah beserta fungsi perubahannya.
3. Perbaikan style, template, trigger, binding, dan resource yang dilakukan.
4. Klasifikasi primer, sekunder, atau destruktif untuk sembilan tombol beserta alasan singkat.
5. Perubahan warna pada Setup Environment dan Uninstall.
6. Hasil build, termasuk error dan warning yang relevan.
7. Hasil pengujian runtime serta state yang benar-benar diperiksa.
8. Hasil pemeriksaan fungsi Uninstall dan batas pengujian yang dilakukan.
9. Masalah yang belum terselesaikan atau belum dapat diverifikasi.

Pisahkan dengan jelas antara pemeriksaan source code, hasil build, dan pengujian runtime.

Jangan mengarang hasil, jangan menyatakan semua pengujian lolos tanpa bukti, dan jangan menyembunyikan keterbatasan lingkungan pengujian.

---

## TARGET AKHIR

Loka memiliki identitas visual yang konsisten dengan putih dan biru sebagai dua warna utama, abu-abu untuk permukaan dan teks netral, serta warna semantik secara terbatas.

- Tombol primer biru selalu memiliki teks putih.
- Tombol sekunder menggunakan teks gelap.
- Tombol destruktif menggunakan merah secara tepat.
- Checkbox, dropdown, input, setup wizard, dan dialog mengikuti aturan warna yang sama.
- Halaman Uninstall, daftar aplikasi, tombol penghapusan, dialog konfirmasi, progress, dan notifikasi memiliki tampilan yang konsisten.
- Seluruh state kontrol memiliki kontras yang benar.
- Fungsi aplikasi tetap utuh.

**Prioritas tertinggi adalah memperbaiki akar masalah pada implementasi WPF yang benar-benar digunakan aplikasi, memastikan seluruh halaman yang ditargetkan konsisten, lalu membuktikan hasilnya melalui build dan pengujian runtime. Jangan sekadar mengubah warna di source code dan menganggap pekerjaan selesai.**