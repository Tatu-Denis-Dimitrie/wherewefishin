# Fish Recognition Service

Serviciu Python Flask pentru detectarea și recunoașterea peștilor în videoclipuri folosind YOLOv8.

## Funcționalități

- 🐟 Detectarea peștilor în timp real din videoclipuri
- 📊 Statistici complete: număr detecții, tipuri, specia dominantă
- 🎥 Generare video procesat cu bounding boxes și etichete
- 🌐 Codificare optimizată pentru web (H.264/AV1)

## Cerințe

### Python Packages
```bash
pip install -r requirements.txt
```

### FFmpeg (NECESAR pentru compatibilitate web!)

Videoclipurile procesate trebuie re-encodate cu FFmpeg pentru a funcționa în browsere și VSCode.

#### **Windows:**

**Opțiunea 1: Chocolatey (Recomandat)**
```powershell
# Instalează Chocolatey dacă nu îl ai
# Apoi instalează FFmpeg:
choco install ffmpeg
```

**Opțiunea 2: Manual**
1. Descarcă FFmpeg de la: https://www.gyan.dev/ffmpeg/builds/
2. Descarcă `ffmpeg-release-essentials.zip`
3. Extrage în `C:\ffmpeg`
4. Adaugă `C:\ffmpeg\bin` în PATH:
   - Start → "Variabile de mediu" → Path → Edit
   - Adaugă: `C:\ffmpeg\bin`
5. Restartează terminalul și testează:
   ```powershell
   ffmpeg -version
   ```

**Opțiunea 3: Winget**
```powershell
winget install Gyan.FFmpeg
```

#### **Linux:**
```bash
# Ubuntu/Debian
sudo apt update
sudo apt install ffmpeg

# Fedora
sudo dnf install ffmpeg

# Arch
sudo pacman -S ffmpeg
```

#### **macOS:**
```bash
brew install ffmpeg
```

## Configurare Video Encoding

În `app.py`, poți configura:

```python
USE_FFMPEG_REENCODE = True  # Activează/dezactivează FFmpeg
USE_AV1_CODEC = False       # True = AV1, False = H.264
```

### Codec-uri disponibile:

- **H.264 (libx264)** - **RECOMANDAT** ✅
  - Compatibilitate maximă cu toate browserele
  - Viteza de encodare rapidă
  - Suport excelent pentru streaming
  - Dimensiune fișier rezonabilă

- **AV1 (libaom-av1)** ⚠️
  - Compresie superioară (fișiere mai mici)
  - Encodare MULT mai lentă (3-10x)
  - Suport limitat în browsere vechi
  - Bun pentru arhivare, nu pentru procesare live

## Pornire serviciu

```bash
# Activează environment-ul virtual
.\venv\Scripts\Activate.ps1  # Windows
source venv/bin/activate      # Linux/macOS

# Pornește serviciul
python app.py
```

Serviciul va rula pe: **http://localhost:5001**

## Endpoints

- `GET /` - Informații serviciu și status FFmpeg
- `GET /health` - Health check
- `GET /api/supported-fish` - Listă tipuri de pești suportați
- `POST /api/analyze-video` - Analizează video (upload)
  - Form-data:
    - `video`: fișier video
    - `use_ffmpeg`: "true"/"false" (opțional, default: true)
    - `use_av1`: "true"/"false" (opțional, default: false)
- `GET /outputs/{filename}` - Descarcă video procesat
- `GET /uploads/{filename}` - Descarcă video original

## Verificare FFmpeg

```bash
# Testează dacă FFmpeg este instalat
ffmpeg -version

# Sau accesează:
http://localhost:5001/
```

Dacă FFmpeg NU este instalat, vei vedea warning-uri în consolă și videoclipurile pot să **nu funcționeze în browsere**.

## Troubleshooting

### Videoclipurile nu se văd în browser
- ✅ Verifică dacă FFmpeg este instalat: `ffmpeg -version`
- ✅ Verifică configurația în `app.py`: `USE_FFMPEG_REENCODE = True`
- ✅ Verifică console-ul pentru warning-uri FFmpeg

### Encodarea este prea lentă
- Schimbă de la AV1 la H.264: `USE_AV1_CODEC = False`
- Sau ajustează preset-ul în funcția `reencode_video_with_ffmpeg()`

### FFmpeg nu este găsit
- Verifică că este în PATH: `echo $env:Path` (Windows) sau `echo $PATH` (Linux/macOS)
- Restartează terminalul după instalare
- Pe Windows, poate fi necesar restart sistem

## Performanță

- **OpenCV encoding**: Rapid, dar videoclipuri incompatibile web
- **H.264 encoding**: Adaugă ~10-30% timp procesare, compatibilitate 100%
- **AV1 encoding**: Adaugă 200-500% timp procesare, compresie optimă

**Pentru producție: folosește H.264!** 🎯
