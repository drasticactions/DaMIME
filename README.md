# DaMIME

DaMIME is a MIME type and content detector, based on [Marcel](https://github.com/rails/marcel) and [Apache Tika](https://tika.apache.org). I was writing small byte-based detection for file types for my AtProtocol/Bluesky library [FishyFlip](https://github.com/drasticactions/fishyflip) and figured it could be nice to have something be more robust.

** NOTE: ** I offer no support for this library. PRs and issues are welcome but don't expect fixes or regular releases. If you find that you depend on this, you should hard fork it outright.

## Build

```bash
dotnet build DaMIME/DaMIME.csproj
```

## Tests

```bash
dotnet test DaMIME-tests/DaMIME-tests.csproj
```

## Usage

### Basic Detection

```csharp
using DaMIME;

// Detect from file extension
string type = MimeType.For(extension: "jpg");
// => "image/jpeg"

// Detect from filename
type = MimeType.For(name: "photo.png");
// => "image/png"

// Detect from stream content (magic bytes)
using var stream = File.OpenRead("document.pdf");
type = MimeType.For(stream);
// => "application/pdf"
```

### Combined Detection

```csharp
// Combine stream + filename for best accuracy
using var stream = File.OpenRead("image.png");
string type = MimeType.For(stream, name: "image.png");
// => "image/png"

// Magic bytes override wrong extension
var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
using var stream = new MemoryStream(pngBytes);
type = MimeType.For(stream, name: "fake.jpg");
// => "image/png" (correctly detected, despite .jpg name)
```

### Content-Type Header Parsing

```csharp
// Parse Content-Type headers
string contentType = "text/html; charset=utf-8";
string mimeType = MimeType.ParseMediaType(contentType);
// => "text/html"
```

### Type Hierarchy

```csharp
// Adobe Illustrator files are PDFs with .ai extension
var pdfBytes = Encoding.ASCII.GetBytes("%PDF-1.4");
using var stream = new MemoryStream(pdfBytes);
string type = MimeType.For(stream, extension: "ai");
// => "application/illustrator" (child of application/pdf)

// Check type relationships
bool isChild = Magic.IsChildOf("text/csv", "text/plain");
// => true
```

### Advanced: Low-Level API

```csharp
// Direct extension lookup
string? type = Magic.ByExtension("mp4");
// => "video/mp4"

// Direct magic byte detection
using var stream = File.OpenRead("audio.mp3");
string? type = Magic.ByMagic(stream);
// => "audio/mpeg"

// Get all matching types
string[] allTypes = Magic.AllByMagic(stream);
```