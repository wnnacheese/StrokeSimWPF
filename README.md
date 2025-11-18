# Stroke Recovery Progress Monitoring

Stroke Recovery Progress Monitoring adalah aplikasi WPF (.NET 8, MVVM) yang mensimulasikan empat kanal sensor rehabilitasi (IMU, FSR, strain gauge, EMG). Engine deterministik 100 Hz memberi makan sepuluh plot ScottPlot v5 (4 domain waktu, 4 FFT, 1 Bode, 1 pole-zero). Panel kontrol menggunakan slider berukuran besar dengan batas numerik jelas, sementara sidebar menyoroti status gabungan dan preset aktif.

## Daftar Isi

1. [Kebutuhan Sistem](#kebutuhan-sistem)
2. [Struktur Folder](#struktur-folder)
3. [Cara Menjalankan](#cara-menjalankan)
4. [Ringkasan UI](#ringkasan-ui)
5. [Run Preset Modes](#run-preset-modes)
6. [Satuan Parameter Sensor](#satuan-parameter-sensor)
7. [Persamaan Sensor & Sistem](#persamaan-sensor--sistem)
8. [Skenario Default](#skenario-default)
9. [Persistensi & Determinisme](#persistensi--determinisme)

## Kebutuhan Sistem

- Windows 10/11 x64
- .NET SDK 8.0 (atau Visual Studio 2022 17.9+ dengan workload Desktop development with C#)
- GPU dengan akselerasi WPF (opsional namun membantu rendering ScottPlot)

## Struktur Folder

Seluruh sumber kini berada di akar `SPS.App/`:

```
SPS.App/
|- Assets/       -> ikon dan gambar UI
|- DSP/          -> utilitas DSP (bilinear transform, FFT helper)
|- Models/       -> definisi parameter & transfer function
|- Services/     -> SignalEngine, penyimpanan JSON, FFT service
|- ViewModels/   -> MainWindowViewModel, plot VM, RelayCommand
|- Views/        -> XAML panel sensor, plot host, parameter row
|- SPS.App.csproj (proyek WPF utama)
|- README.md, App.xaml, dll.
```

## Cara Menjalankan

1. Buka terminal di folder `SPS.App/`.
2. Jalankan `dotnet restore`.
3. Jalankan `dotnet build`.
4. Jalankan `dotnet run` (atau buka `SPS.App.sln` di Visual Studio dan tekan F5).

## Ringkasan UI

### Transport dan Status
- **Start / Stop / Reset** mengendalikan engine 100 Hz dan plot loop DispatcherTimer. Reset membersihkan buffer, memulihkan parameter preset/JSON, dan menyegarkan status gabungan.
- **Status Buffer** menampilkan indikator warna (hijau/kuning/merah) berdasarkan fill ring buffer serta teks FPS dan peringatan clamp.

### Panel Sensor
- Preset Baseline Stable / Fast Exercise / Drift Bias tersedia dalam bentuk tombol pil tepat di bawah transport.
- Tab IMU/FSR/Strain/EMG memakai slider tinggi dengan step kecil/besar, tombol +/- cepat, dan kotak angka yang langsung mengikat ke parameter bersama.
- Perubahan slider ditahan sekitar 100 ms sebelum pipeline transfer dihitung ulang agar UI tetap stabil.

### Plot Domain Waktu
- **Combined Time (raw)** menampilkan IMU (deg), FSR (V), Strain (V), dan EMG (a.u.) tanpa normalisasi sehingga efek preset terlihat langsung.
- **Plot sensor spesifik** menggunakan jendela 5 detik dengan rentang tetap: IMU 0-90 deg, FSR 2.5-3.5 V, Strain 0-0.004 V, EMG 0-2.5 a.u., lengkap dengan overlay teks instan.

### FFT dan Analisis Frekuensi
- FFT tiap kanal diperbarui hingga 10 Hz dan menyorot empat puncak dominan.
- Panel Strain/EMG menampilkan overlay gabungan untuk membandingkan vibrasi mekanik vs aktivitas otot.

### Kartu Gabungan & Bode
- Bobot gabungan tetap [1,1,1,1]; kartu menampilkan formula singkat, status stabilitas (mis. max |z| = 0.83), dan pesan ketika sistem tidak stabil. Baris sistem (paling bawah) memuat Bode (kolom 0-1) serta kartu pole-zero bertab Z-domain/S-domain di kolom 2.
- Transfer function dihitung ulang setelah parameter idle 200 ms. Plot Bode/pole-zero menambahkan kurva "Combined" hanya bila sistem stabil.

### Panel Parameter & Banner
- Banner preset menampilkan ringkasan seperti "Baseline Stable: theta 15 deg | FSR 3.17 V | Strain 0.003 V | EMG 0.12 a.u." dan diperbarui tiap detik.
- Bagian parameter menyajikan slider masing-masing sensor yang dapat digulir.

### Model FSR (contoh)
- Parameter Force amplitude/offset, model a/b, dan Rmin langsung mempengaruhi tegangan:

```
Force = ForceOffset + ForceAmplitude
Resistance = 1 / (a * Force^b) + Rmin
Vout = 3.3 V * 10k / (10k + Resistance)
```

## Run Preset Modes

Preset berada tepat di bawah tombol Start/Stop/Reset agar operator tinggal memilih skenario tanpa memindah slider manual. Setiap preset mensimulasikan episode rehabilitasi spesifik.

| Preset            | IMU (amp, offset, freq) | FSR (amp, bias, a, b, Rmin) | Strain (offset, amp) | EMG (amp, activation) | Skenario simulasi |
|-------------------|-------------------------|-----------------------------|----------------------|-----------------------|-------------------|
| Baseline Stable   | 15 deg / 0 deg / 2.5 Hz | 10 N / 5 N / 0.4 / 0.8 / 400 ohm | 150 microstrain / 100 microstrain | 0.5 / 0.25 | Latihan ringan dengan tekanan telapak rendah dan kontraksi otot kecil, cocok untuk kalibrasi awal. |
| Fast Exercise     | 60 deg / 5 deg / 6 Hz   | 60 N / 20 N / 0.25 / 0.7 / 200 ohm | 300 microstrain / 500 microstrain | 2.5 / 0.8 | Latihan resistif cepat: IMU menyapu sudut besar, FSR menerima ketukan kuat berulang, strain tinggi, EMG mendekati kontraksi maksimum. |
| Drift Bias        | 20 deg / 30 deg / 0.5 Hz | 15 N / 40 N / 0.15 / 1.1 / 1000 ohm | 500 microstrain / 200 microstrain | 1.0 / 0.4 | Skenario troubleshooting bias: IMU memiliki offset besar, FSR menahan gaya statis, strain dan EMG menunjukkan drift. |
| Custom            | mengikuti slider        | mengikuti slider             | mengikuti slider    | mengikuti slider      | Aktif otomatis saat pengguna mengubah parameter manual atau memuat konfigurasi JSON. |

Catatan implementasi:
1. **Tujuan simulasi** - tiga preset bawaan mewakili baseline deterministik, latihan agresif, dan kasus bias.
2. **ApplyPreset & ParamsBus** - `ParametersStore.ApplyPreset` menulis langsung ke objek parameter yang dibagi view, lalu `ParamsBus` menyiarkan perubahan agar SignalEngine dan plot menghitung ulang.
3. **Banner preset** - diperbarui setiap detik untuk menampilkan keluaran IMU/FSR/Strain/EMG hasil preset.
4. **Persistensi** - snapshot JSON di `%LOCALAPPDATA%/StrokeRecovery/parameters.json` akan memaksa mode **Custom** sampai preset lain dipilih.

## Satuan Parameter Sensor

| Sensor | Parameter                    | Satuan                     |
|--------|------------------------------|----------------------------|
| IMU    | Amplitudo, offset/drift      | derajat (deg, deg/s)       |
|        | Frekuensi                    | hertz (Hz)                 |
|        | Redaman zeta                 | tak berdimensi             |
| FSR    | Force amplitude / bias       | newton (N)                 |
|        | Model a, b                   | tak berdimensi             |
|        | Rmin                         | ohm                        |
| Strain | Offset & amplitude           | microstrain                |
|        | Tegangan                     | volt (V)                   |
| EMG    | Amplitudo & activation       | amplitudo relatif (a.u.)   |

## Persamaan Sensor & Sistem

Setiap kanal dibangkitkan dari persamaan deterministik berikut sehingga hasil simulasi konsisten antar sesi.

### IMU

```
theta(t) = OffsetDeg + AmplitudeDeg * sin(2 * pi * FrequencyHz * t)
```

- theta(t): sudut derajat.
- OffsetDeg: bias statis (rentang -180 sampai 180 deg).
- AmplitudeDeg: amplitudo sinus (0 sampai 180 deg).
- FrequencyHz: frekuensi gerak (0 sampai 20 Hz); frekuensi 0 menghasilkan sinyal DC OffsetDeg + AmplitudeDeg.
- t: titik waktu pada jendela buffer 5 detik.

### FSR

```
Force = ForceOffset + ForceAmplitude
R(Force) = 1 / (a * Force^b) + Rmin
Vout = Vcc * Rfixed / (Rfixed + R(Force))
```

- ForceOffset, ForceAmplitude: gaya dasar dan tambahan.
- a, b: koefisien power-law sensor.
- Rmin: resistansi minimum.
- Vcc dan Rfixed: parameter (default 3.3 V dan 10 kOhm) sehingga pembagi tegangan sepenuhnya parameter-driven.
- Vout: tegangan keluaran yang dikirim ke plot FSR.

### Strain Gauge

```
strain = (EpsilonOffsetMicro + EpsilonAmplitudeMicro) * 1e-6
DeltaV / Vexc = (GF / 4) * strain
Vout = (GF / 4) * strain * ExcitationVoltage
```

- EpsilonOffsetMicro, EpsilonAmplitudeMicro: microstrain.
- GF: gauge factor.
- ExcitationVoltage: tegangan bridge (0 sampai 10 V).
- Vout: tegangan keluaran kanal strain.

### EMG

```
EMG(t) = Amplitude * ActivationLevel
```

- Amplitude: level maksimum (a.u.).
- ActivationLevel: duty 0 sampai 1.
- EMG berupa DC, berubah hanya ketika parameter diperbarui.

### Sistem Gabungan

```
G_total(s) = G_IMU(s) + G_FSR(s) + G_Strain(s) + G_EMG(s)
```

Setiap G_sensor(s) berasal dari model analog (IMU orde-2, kanal lain orde-1). TransformsService mendiskretkan G_total(s) dengan bilinear transform sehingga bobot unity [1,1,1,1] dapat dipetakan ke domain z, dianalisis pada plot Bode/pole-zero, dan diuji stabilitasnya.
- S-domain: pole-zero analog dihitung per sensor dan gabungan, ditampilkan pada tab S-domain.
- Z-domain: hasil bilinear/ZOH ditampilkan pada tab Z-domain dan menjadi dasar cek stabilitas (|pole| < 1).

## Skenario Default

Saat aplikasi pertama kali dijalankan dan tombol **Start** ditekan, simulasi memuat konfigurasi deterministik:

1. **Kondisi Parameter** - Aplikasi memulai preset Baseline Stable (atau memuat nilai dari snapshot JSON) sehingga semua sensor langsung mengikuti konfigurasi deterministik tersebut.
2. **Tanpa Noise** - Engine tidak lagi menambahkan noise atau drift; setiap slider/preset menghasilkan sinyal deterministik sesuai rumus.
3. **Kondisi Model Stabil** - Dengan bobot unity, semua pole gabungan berada di dalam unit circle; status akan berubah menjadi UNSTABLE bila kombinasi preset/slider memindahkan pole keluar.

## Persistensi & Determinisme

- JsonStorage menyimpan snapshot parameter di `%LOCALAPPDATA%/StrokeRecovery/parameters.json`. Jika file ada, nilainya dimuat dan preset otomatis menjadi **Custom**.
- SignalEngine bebas RNG/perlin noise sehingga setiap preset menghasilkan respons identik antar sesi.
