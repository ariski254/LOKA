using System.IO;

namespace Loka.Project;

public static class LocalizationScaffolder
{
    public static async Task ScaffoldLaravelIndonesianLocaleAsync(string projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath) || !Directory.Exists(projectPath)) return;

        var langDir = Path.Combine(projectPath, "lang", "id");
        Directory.CreateDirectory(langDir);

        // 1. validation.php
        var validationPath = Path.Combine(langDir, "validation.php");
        var validationContent = @"<?php

return [
    'accepted' => 'Kolom :attribute harus diterima.',
    'accepted_if' => 'Kolom :attribute harus diterima jika :other adalah :value.',
    'active_url' => 'Kolom :attribute bukan URL yang valid.',
    'after' => 'Kolom :attribute harus berupa tanggal setelah :date.',
    'alpha' => 'Kolom :attribute hanya boleh berisi huruf.',
    'alpha_dash' => 'Kolom :attribute hanya boleh berisi huruf, angka, tanda hubung, dan garis bawah.',
    'alpha_num' => 'Kolom :attribute hanya boleh berisi huruf dan angka.',
    'array' => 'Kolom :attribute harus berupa sebuah array.',
    'boolean' => 'Kolom :attribute harus bernilai true atau false.',
    'confirmed' => 'Konfirmasi kolom :attribute tidak cocok.',
    'current_password' => 'Kata sandi saat ini salah.',
    'date' => 'Kolom :attribute bukan tanggal yang valid.',
    'email' => 'Kolom :attribute harus berupa alamat surel yang valid.',
    'exists' => 'Kolom :attribute yang dipilih tidak valid.',
    'file' => 'Kolom :attribute harus berupa file.',
    'filled' => 'Kolom :attribute wajib diisi.',
    'image' => 'Kolom :attribute harus berupa gambar.',
    'integer' => 'Kolom :attribute harus berupa bilangan bulat.',
    'ip' => 'Kolom :attribute harus berupa alamat IP yang valid.',
    'json' => 'Kolom :attribute harus berupa string JSON yang valid.',
    'max' => [
        'numeric' => 'Kolom :attribute tidak boleh lebih dari :max.',
        'file' => 'Kolom :attribute tidak boleh lebih dari :max kilobita.',
        'string' => 'Kolom :attribute tidak boleh lebih dari :max karakter.',
        'array' => 'Kolom :attribute tidak boleh lebih dari :max item.',
    ],
    'min' => [
        'numeric' => 'Kolom :attribute minimal harus :min.',
        'file' => 'Kolom :attribute minimal harus :min kilobita.',
        'string' => 'Kolom :attribute minimal harus :min karakter.',
        'array' => 'Kolom :attribute minimal harus memiliki :min item.',
    ],
    'numeric' => 'Kolom :attribute harus berupa angka.',
    'password' => [
        'letters' => 'Kolom :attribute harus berisi setidaknya satu huruf.',
        'mixed' => 'Kolom :attribute harus berisi setidaknya satu huruf besar dan satu huruf kecil.',
        'numbers' => 'Kolom :attribute harus berisi setidaknya satu angka.',
        'symbols' => 'Kolom :attribute harus berisi setidaknya satu simbol.',
    ],
    'required' => 'Kolom :attribute wajib diisi.',
    'same' => 'Kolom :attribute dan :other harus sama.',
    'string' => 'Kolom :attribute harus berupa string.',
    'unique' => 'Kolom :attribute sudah ada sebelumnya.',
    'url' => 'Kolom :attribute harus berupa URL yang valid.',
    'attributes' => [],
];
";
        await File.WriteAllTextAsync(validationPath, validationContent);

        // 2. auth.php
        var authPath = Path.Combine(langDir, "auth.php");
        var authContent = @"<?php

return [
    'failed' => 'Kredensial yang diberikan tidak cocok dengan data kami.',
    'password' => 'Kata sandi yang dimasukkan salah.',
    'throttle' => 'Terlalu banyak percobaan masuk. Silakan coba lagi dalam :seconds detik.',
];
";
        await File.WriteAllTextAsync(authPath, authContent);

        // 3. pagination.php
        var paginationPath = Path.Combine(langDir, "pagination.php");
        var paginationContent = @"<?php

return [
    'previous' => '&laquo; Sebelumnya',
    'next' => 'Berikutnya &raquo;',
];
";
        await File.WriteAllTextAsync(paginationPath, paginationContent);

        // 4. passwords.php
        var passwordsPath = Path.Combine(langDir, "passwords.php");
        var passwordsContent = @"<?php

return [
    'reset' => 'Kata sandi Anda telah disetel ulang.',
    'sent' => 'Kami telah mengirimkan tautan setel ulang kata sandi ke alamat surel Anda.',
    'throttled' => 'Harap tunggu beberapa saat sebelum mencoba kembali.',
    'token' => 'Token setel ulang kata sandi ini sudah tidak berlaku.',
    'user' => 'Kami tidak dapat menemukan pengguna dengan alamat surel tersebut.',
];
";
        await File.WriteAllTextAsync(passwordsPath, passwordsContent);
    }

    public static async Task ScaffoldCodeIgniterIndonesianLocaleAsync(string projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath) || !Directory.Exists(projectPath)) return;

        var langDir = Path.Combine(projectPath, "app", "Language", "id");
        Directory.CreateDirectory(langDir);

        // 1. Validation.php
        var validationPath = Path.Combine(langDir, "Validation.php");
        var validationContent = @"<?php

return [
    'required' => 'Kolom {field} wajib diisi.',
    'matches' => 'Kolom {field} tidak cocok dengan kolom {param}.',
    'valid_email' => 'Kolom {field} harus berisi alamat surel yang valid.',
    'min_length' => 'Kolom {field} minimal harus memiliki {param} karakter.',
    'max_length' => 'Kolom {field} tidak boleh melebihi {param} karakter.',
    'exact_length' => 'Kolom {field} harus tepat sepanjang {param} karakter.',
    'numeric' => 'Kolom {field} hanya boleh berisi angka.',
    'integer' => 'Kolom {field} harus berupa bilangan bulat.',
    'decimal' => 'Kolom {field} harus berupa bilangan desimal.',
    'is_natural' => 'Kolom {field} hanya boleh berisi bilangan asli.',
    'is_natural_no_zero' => 'Kolom {field} harus berisi angka yang lebih besar dari nol.',
    'is_unique' => 'Kolom {field} harus berisi nilai unik yang belum terdaftar.',
    'valid_url' => 'Kolom {field} harus berisi URL yang valid.',
    'alpha' => 'Kolom {field} hanya boleh berisi karakter alfabet.',
    'alpha_numeric' => 'Kolom {field} hanya boleh berisi karakter alfanumerik.',
];
";
        await File.WriteAllTextAsync(validationPath, validationContent);

        // 2. App.php
        var appPath = Path.Combine(langDir, "App.php");
        var appContent = @"<?php

return [
    'welcome' => 'Selamat Datang di CodeIgniter 4',
    'pageNotFound' => 'Halaman tidak ditemukan',
    'home' => 'Beranda',
    'environment' => 'Lingkungan Pengembangan',
    'serverTime' => 'Waktu Server',
];
";
        await File.WriteAllTextAsync(appPath, appContent);
    }
}
