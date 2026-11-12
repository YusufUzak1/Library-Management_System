# 📚 Scriptorium — Modern Kütüphane Yönetim & Otomasyon Sistemi

> **Kurumsal düzeyde Clean Architecture prensipleriyle geliştirilmiş, modern WPF arayüzlü ve MariaDB/MySQL destekli kütüphane otomasyon çözümü.**

[![.NET 8.0](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![C# 12](https://img.shields.io/badge/C%23-12.0-239120?logo=csharp&logoColor=white)](https://learn.microsoft.com/dotnet/csharp/)
[![WPF](https://img.shields.io/badge/Platform-WPF%20XAML-blue)](https://learn.microsoft.com/dotnet/desktop/wpf/)
[![Database](https://img.shields.io/badge/Database-MariaDB%20%7C%20MySQL-orange?logo=mariadb&logoColor=white)](https://mariadb.org/)
[![ORM](https://img.shields.io/badge/Data%20Access-Dapper-red)](https://github.com/DapperLib/Dapper)
[![Architecture](https://img.shields.io/badge/Architecture-Clean%20Architecture-brightgreen)](#-mimari-yapı)

---

## 📌 Proje Genel Bakış

**Scriptorium**, modern kütüphanelerin, üniversitelerin ve kurumsal bilgi merkezlerinin tüm operasyonel ihtiyaçlarını karşılamak üzere tasarlanmış masaüstü kütüphane otomasyon sistemidir. 

Sistem; kitap envanteri, üye yönetimi, ödünç alma/iade süreçleri, gecikme cezası hesaplamaları ve anlık raporlama modüllerini tek bir çatı altında birleştirir. **Rol Tabanlı Erişim Kontrolü (RBAC)** sayesinde hem idari personel için detaylı bir yönetim konsolu, hem de okuyucular/öğrenciler için görsel olarak zengin bir kiosk deneyimi sunar.

---

## 🏛️ Mimari Yapı (Clean Architecture)

Proje, kurumsal yazılım standartlarına uygun olarak **Temiz Mimari (Clean Architecture)** katmanlarına ayrılmıştır:

```
kutuphane-otomasyonu/
├── 📁 LibraryManagement.Domain           # Çekirdek Varlıklar (Entities, Enums, Value Objects)
├── 📁 LibraryManagement.Application      # İş Kuralları, DTO'lar, Arayüz Sözleşmeleri (Interfaces)
├── 📁 LibraryManagement.Infrastructure   # MariaDB/MySQL Erişimi, Dapper Veri Servisleri, Migration
├── 📁 LibraryManagement.Desktop          # WPF UI Katmanı, MVVM, XAML Görünümleri, Özel Kontroller
└── 📁 LibraryManagement.Tests            # Birim ve Entegrasyon Testleri
```

### Katman Sorumlulukları
1. **Domain:** Dış katmanlardan tamamen bağımsız çekirdek model (`Book`, `Member`, `BorrowRecord`, `ActivityLog` vb.).
2. **Application:** İş akışları ve veri transfer nesneleri.
3. **Infrastructure:** `MySqlConnector` ve `Dapper` ile optimize edilmiş veritabanı iletişimi, otomatik veritabanı oluşturma (`Auto-DDL`) ve tohumlama (`Seed Data`).
4. **Desktop:** XAML tabanlı modern, akıcı ve animasyonlu arayüz. Kütüphane görevlisi ve son kullanıcı için ayrılmış ekranlar.
5. **Tests:** Sistemin kritik iş kurallarını doğrulayan otomatik testler.

---

## ✨ Temel Özellikler

### 1. 🛡️ Rol Tabanlı Giriş, Güvenlik ve Kullanıcı Kayıt Sistemi (RBAC & Self-Registration)
- **Yeni Kullanıcı Kaydı (Self-Registration):** Yeni okuyucular/öğrenciler giriş ekranındaki "Hemen Kayıt Olun" alanından Ad-Soyad, Kullanıcı Adı, E-posta, Telefon, Üye Türü (Öğrenci, Akademik, Personel) ve Şifre ile sisteme anında kayıt olabilir.
- **Otomatik Üye Entegrasyonu:** Kayıt olan her kullanıcı için sistem otomatik olarak benzersiz kütüphane üye numarası (`FU-2026-XXXX`) tanımlar, veritabanına ve aktivite günlüğüne kaydeder.
- **Yönetici (Admin) Modu:** Tüm kütüphane envanterini, üye verilerini, ödünç hareketlerini ve sistem loglarını tam yetkiyle yönetir.
- **Kullanıcı / Kiosk Modu:** Ziyaretçilerin kitap araması yapabileceği, mevcut durumları inceleyebileceği ve kendi adlarına hızlı ödünç işlemi gerçekleştirebileceği modern self-servis arayüz.

### 2. 📖 Kitap & Katalog Yönetimi
- ISBN, Barkod, Dewey Onlu Sınıflama Kodu (DDC) takibi.
- Yazar, yayınevi, kategori, basım yılı, sayfa sayısı ve raf lokasyonu (örn: `A-01`, `B-03`) bilgileri.
- Anlık stok kontrolü: Toplam adet, raftaki mevcut kopya ve rezerve/ödünçteki adet takibi.

### 3. 👥 Üye & Okuyucu Yönetimi
- Üye kartı numarası, T.C. Kimlik / Kurum Sicil No, iletişim bilgileri ve üye türü (Öğrenci, Akademisyen, Personel).
- Üye bazlı maksimum ödünç limiti denetimi.
- Aktif ödünç sayısı, geçmiş ödünçler ve toplam bakiye/ceza takibi.

### 4. 🔄 Ödünç Verme & İade Döngüsü
- Tek tıkla ödünç verme ve teslim tarihi ataması.
- Geciken iadeler için otomatik gün bazlı gecikme cezası hesabı.
- İade alınan kitabın otomatik olarak raf stoğuna geri dahil edilmesi.

### 5. 🔌 Akıllı Veritabanı Entegrasyonu & Kesintisiz Çalışma (Resilience)
- Sistem başlangıçta yerel MariaDB / MySQL servisine otomatik bağlanır; veritabanı ve tablolar mevcut değilse otomatik olarak oluşturur (`Auto-Provisioning`).
- Veritabanı servisinin kapalı olduğu ortamlarda uygulama çökmez; **akıllı fallback** mekanizması devreye girerek yerel bellek modu üzerinden kesintisiz çalışmayı sürdürür.

---

## 🛠️ Kullanılan Teknolojiler

| Alan | Teknoloji / Kütüphane | Açıklama |
| :--- | :--- | :--- |
| **Platform** | .NET 8.0 SDK | Modern, yüksek performanslı LTS platform |
| **Dil** | C# 12 | Güncel dil özellikleri, pattern matching, records |
| **Arayüz (UI)** | WPF (Windows Presentation Foundation) | Donanım hızlandırmalı XAML vektörel arayüz |
| **Veritabanı** | MariaDB 11+ / MySQL 8+ | Güvenilir ve ilişkisel veri saklama |
| **Veri Erişimi** | Dapper 2.1 & MySqlConnector 2.4 | Hafif siklet, yüksek hızlı Micro-ORM |
| **Test** | VSTest / MSTest / xUnit | Otomatik test çalıştırma ve doğrulama |

---

## 🚀 Hızlı Başlangıç & Kurulum

### Önkoşullar
- Windows 10/11 x64
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- *(İsteğe Bağlı)* MariaDB veya MySQL (XAMPP / Docker / Doğrudan Kurulum)
  - Not: Veritabanı kurulu olmasa dahi uygulama demo modunda otomatik açılır.

### Kurulum Adımları

1. **Depoyu Klonlayın:**
   ```bash
   git clone https://github.com/YusufUzak1/Library-Management_System.git
   cd Library-Management_System
   ```

2. **Bağımlılıkları Geri Yükleyin ve Derleyin:**
   ```bash
   dotnet restore
   dotnet build LibraryManagementSystem.sln --configuration Release
   ```

3. **Uygulamayı Çalıştırın:**
   ```bash
   # Alternatif 1: Doğrudan bat dosyası ile
   Calistir.bat

   # Alternatif 2: .NET CLI ile
   dotnet run --project LibraryManagement.Desktop/LibraryManagement.Desktop.csproj
   ```

4. **Testleri Çalıştırın:**
   ```bash
   dotnet test LibraryManagementSystem.sln
   ```

---

## 🔑 Varsayılan Giriş Bilgileri

Sistem ilk açılışta aşağıdaki demo kullanıcılarıyla doğrudan test edilebilir:

| Rol | Kullanıcı Adı | Şifre | Yetki Kapsamı |
| :--- | :--- | :--- | :--- |
| **Yönetici (Admin)** | `admin` | `1234` | Tam Yetki (Yönetim Paneli, Kitap/Üye/İşlem Yönetimi) |
| **Kullanıcı (User)** | `kullanici` | `1234` | Kiosk Görünümü (Kitap Arama, Ödünç Alma, Profil) |

---

## 📂 Proje Dizin Hiyerarşisi

```
├── 📁 LibraryManagement.Application/       # Uygulama servis katmanı
├── 📁 LibraryManagement.Desktop/           # Masaüstü uygulaması ve XAML pencereleri
│   ├── 📄 LoginWindow.xaml                 # Rol seçimli modern giriş ekranı
│   ├── 📄 MainWindow.xaml                  # Yönetim paneli ana penceresi
│   ├── 📄 Customer3DWindow.xaml            # Kullanıcı self-servis kiosk penceresi
│   └── 📁 Assets/                          # Görseller ve ikonlar
├── 📁 LibraryManagement.Domain/            # Varlık ve model tanımları
├── 📁 LibraryManagement.Infrastructure/    # Veritabanı ve veri erişim katmanı
├── 📁 LibraryManagement.Tests/             # Otomasyon testleri
├── 📄 LibraryManagementSystem.sln          # Visual Studio çözüm dosyası
├── 📄 Calistir.bat                         # Hızlı başlatma betiği
└── 📄 README.md                            # Proje dokümantasyonu
```

---

## 👨‍💻 Geliştirici & İletişim

- **Geliştirici:** Yusuf Uzak
- **GitHub:** [@YusufUzak1](https://github.com/YusufUzak1)
- **Depo Adresi:** [https://github.com/YusufUzak1/Library-Management_System](https://github.com/YusufUzak1/Library-Management_System)

---
*Bu proje modern yazılım mimarisi prensipleri ve kurumsal standartlara sadık kalınarak hazırlanmıştır.*

