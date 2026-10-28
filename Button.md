## AUDIT WAJIB: TOMBOL YANG WARNANYA TIDAK BERUBAH

**MASALAH KRITIS:** Perbaikan warna sebelumnya tidak menghasilkan perubahan visual pada aplikasi Loka. Jangan mengulangi pendekatan yang sama. Jangan hanya mengubah resource atau menambahkan style baru tanpa memastikan bahwa style tersebut benar-benar diterapkan pada tombol yang terlihat di aplikasi.

Audit dan perbaiki secara individual seluruh tombol berikut:

1. `Setup Environment`
2. `Continue`
3. `Apply Setup`
4. `Open Loka Dashboard`
5. `New`
6. `Activate`
7. `Create`
8. `Run Query`
9. `Save Changes`
10. `Uninstall`
11. `Refresh`
12. `Search`
13. `Filter`
14. `Back`
15. `Cancel`

Daftar ini merupakan target pemeriksaan wajib, bukan daftar lengkap. Cari juga tombol lain di seluruh aplikasi, termasuk halaman utama, setup wizard, project, server, database, pengaturan, dialog, dan halaman Uninstall.

### 1. Periksa setiap tombol secara nyata

Untuk setiap tombol:

- Temukan deklarasi XAML atau kode C# yang membuatnya.
- Identifikasi `x:Name`, `AutomationProperties.Name`, `Content`, binding, resource key, command, dan event handler yang relevan.
- Catat style, template, foreground, background, border, serta trigger yang benar-benar diterapkan.
- Telusuri apakah style ditimpa oleh implicit style, explicit style, `BasedOn`, `ControlTemplate`, trigger, code-behind, atau custom control.
- Periksa apakah teks tombol berasal dari `TextBlock`, `ContentPresenter`, template khusus, atau elemen anak yang menetapkan foreground sendiri.
- Tentukan apakah tombol termasuk primer, sekunder, destruktif, atau aksi netral berdasarkan fungsi aktualnya.

Jangan mengubah semua tombol menjadi biru. Terapkan hierarki warna yang sesuai dengan fungsi setiap tombol.

### 2. Gunakan aturan warna yang eksplisit

**Tombol primer:**
- Normal: background `#2563EB`, teks `#FFFFFF`.
- Hover: background `#1D4ED8`, teks `#FFFFFF`.
- Pressed: background biru lebih gelap, teks `#FFFFFF`.
- Focused: foreground tetap sesuai jenis tombol.
- Disabled: background `#F4F4F5`, teks `#A1A1AA`.

**Tombol sekunder:**
- Normal: background `#FFFFFF`, teks `#18181B`, border `#E4E4E7`.
- Hover: background `#F4F4F5`, teks `#18181B`.
- Pressed: background abu-abu netral, teks `#18181B`.
- Disabled: background `#F4F4F5`, teks `#A1A1AA`.

**Tombol destruktif:**
- Normal: background putih atau transparan, teks `#DC2626`.
- Hover: background `#FEF2F2`.
- Disabled: warna netral yang tetap terbaca.

Ikon di dalam tombol harus mengikuti foreground yang sesuai, kecuali ikon status semantik.

### 3. Periksa mengapa perubahan sebelumnya tidak terlihat

Jangan langsung menganggap masalahnya hanya pada `Foreground`. Selidiki kemungkinan berikut:

- File XAML yang diubah bukan file yang digunakan oleh halaman aktif.
- Style baru tidak pernah dimuat atau direferensikan.
- `MergedDictionaries` memuat resource yang berbeda atau saling menimpa.
- Terdapat beberapa style dengan key sama dan style yang aktif bukan yang diperbaiki.
- `ControlTemplate` mengabaikan atau menimpa `Foreground` dari tombol.
- `TextBlock` di dalam tombol memiliki foreground hardcoded.
- Trigger `IsMouseOver`, `IsPressed`, `IsEnabled`, atau focus mengubah warna kembali.
- Code-behind C# menetapkan warna setelah XAML dimuat.
- Tombol dibuat secara dinamis atau menggunakan custom control.
- Aplikasi yang dijalankan menggunakan executable lama.
- Build menggunakan project, konfigurasi, atau direktori output yang berbeda.
- Proses aplikasi lama masih berjalan sehingga perubahan terbaru tidak terlihat.

Periksa bukti dari implementasi aktual sebelum menentukan penyebabnya. Jangan sekadar memilih salah satu kemungkinan tanpa verifikasi.

### 4. Perbaiki akar masalah dan pastikan style diterapkan

Setelah penyebab ditemukan:

1. Perbaiki sumber masalah pada style, template, trigger, binding, atau kode yang benar-benar mengendalikan tombol.
2. Gunakan resource terpusat jika sesuai dengan arsitektur project.
3. Pastikan style tersebut benar-benar digunakan oleh seluruh tombol yang ditargetkan.
4. Jika sebuah tombol memiliki template khusus, perbaiki template tersebut tanpa merusak fungsi dan layout-nya.
5. Jika style global merupakan sumber masalah, perbaiki secara terpusat dengan mempertahankan tampilan kontrol lain yang sudah benar.
6. Jangan menambahkan style duplikat, resource tidak terpakai, atau override acak hanya untuk membuat perubahan tampak di source code.
7. Pertahankan command, event handler, binding, validasi, ukuran, margin, padding, alignment, navigasi, dan perilaku tombol.

### 5. Terapkan pemeriksaan yang sama pada Setup dan Uninstall

Pastikan warna tombol pada Setup Environment dan seluruh fitur Uninstall juga benar-benar berubah ketika aplikasi dijalankan.

Periksa checkbox, dropdown, input, daftar aplikasi, baris terpilih, dialog konfirmasi Uninstall, progress bar, dan notifikasi.

- Background utama: `#FFFFFF`.
- Panel sekunder: `#FAFAFA`.
- Seleksi: `#F0F6FF`.
- Border: `#E4E4E7`.
- Teks utama: `#18181B`.
- Teks sekunder: `#71717A`.
- Aksi primer: biru dengan teks putih.
- Aksi sekunder: putih dengan teks gelap.
- Uninstall: gaya destruktif dengan teks merah.
- Status berhasil, peringatan, dan gagal menggunakan warna semantik yang sesuai.

Jangan mengubah fungsi Setup atau Uninstall. Jangan menyembunyikan aplikasi dari daftar hanya untuk membuat penghapusan terlihat berhasil.

### 6. Build ulang dan verifikasi executable terbaru

Setelah perbaikan:

1. Hentikan instance Loka lama jika aman dilakukan.
2. Build ulang project startup yang benar.
3. Pastikan build menggunakan source terbaru dan tidak memiliki error kompilasi.
4. Jalankan executable yang benar-benar dihasilkan oleh build tersebut.
5. Pastikan tidak ada instance lama yang masih menampilkan UI sebelumnya.
6. Buka setiap halaman yang memuat tombol target.
7. Periksa warna background, foreground, border, dan ikon.
8. Uji normal, hover, pressed, focused, dan disabled apabila state tersebut tersedia.
9. Periksa halaman lain untuk menemukan tombol biru dengan teks gelap atau tombol sekunder dengan teks putih.
10. Periksa binding error, exception, dan regresi fungsi.

Jika memungkinkan, gunakan UI automation atau inspeksi visual runtime untuk membuktikan bahwa warna tombol telah berubah. Jangan menyimpulkan keberhasilan hanya dari perubahan kode XAML.

### 7. Buat laporan verifikasi per tombol

Laporkan hasil dalam tabel dengan kolom berikut:

| Tombol | Style yang diterapkan | Warna normal | State yang diuji | Hasil |
|---|---|---|---|---|
| Setup Environment | Primer/Sekunder | Background dan teks aktual | State yang benar-benar diuji | Lulus/Belum lulus |
| Continue | Primer/Sekunder | Background dan teks aktual | State yang benar-benar diuji | Lulus/Belum lulus |
| Apply Setup | Primer/Sekunder | Background dan teks aktual | State yang benar-benar diuji | Lulus/Belum lulus |
| Open Loka Dashboard | Primer/Sekunder | Background dan teks aktual | State yang benar-benar diuji | Lulus/Belum lulus |
| New | Primer/Sekunder | Background dan teks aktual | State yang benar-benar diuji | Lulus/Belum lulus |
| Activate | Primer/Sekunder | Background dan teks aktual | State yang benar-benar diuji | Lulus/Belum lulus |
| Create | Primer/Sekunder | Background dan teks aktual | State yang benar-benar diuji | Lulus/Belum lulus |
| Run Query | Primer/Sekunder | Background dan teks aktual | State yang benar-benar diuji | Lulus/Belum lulus |
| Save Changes | Primer/Sekunder | Background dan teks aktual | State yang benar-benar diuji | Lulus/Belum lulus |
| Uninstall | Destruktif | Background dan teks aktual | State yang benar-benar diuji | Lulus/Belum lulus |
| Refresh, Search, Filter, Back, Cancel | Sesuai fungsi | Background dan teks aktual | State yang benar-benar diuji | Lulus/Belum lulus |

Tambahkan tombol lain yang ditemukan selama audit. Jangan mengisi hasil berdasarkan perkiraan; jika tombol tidak dapat dibuka atau state tidak dapat diuji, tuliskan keterbatasannya.

## KRITERIA SELESAI

Pekerjaan belum dianggap selesai hanya karena source code berubah atau build berhasil.

Perbaikan dinyatakan selesai jika:

- Akar penyebab warna yang tidak berubah sudah diidentifikasi.
- Style yang benar benar-benar diterapkan oleh UI runtime.
- Semua tombol target sudah diperiksa secara individual.
- Background dan foreground mengikuti hierarki warna yang ditetapkan.
- Setup dan Uninstall tetap berfungsi.
- Executable hasil build terbaru sudah dijalankan.
- Hasil pemeriksaan runtime dilaporkan dengan jujur.

**Prioritas utama: cari tahu mengapa perubahan warna sebelumnya sama sekali tidak terlihat, perbaiki penyebab tersebut, dan buktikan hasilnya pada aplikasi Loka yang benar-benar berjalan.**