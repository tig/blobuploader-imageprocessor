# blobuploader-imageprocessor

Azure Function App for tig/blobuploader.

Implements a set of APIs for converting and sizing images in C#.

## Overview

This Azure Function processes images, resizes them to different dimensions, and uploads them to Azure Blob Storage. It supports:
- Base64-encoded image input
- Customizable dimensions for original, resized, and thumbnail images
- Integration


# **Setting Up the BlobUploader ImageProcessor Repository**

Follow these steps to set up the repository and run the Azure Function locally.

---

## **Prerequisites**

### **1. Install Required Tools**
Before setting up the repository, ensure the following tools are installed:

1. **[Visual Studio Code](https://code.visualstudio.com/Download)**
2. **Azure Functions Core Tools**
   - Install via npm:
     ```bash
     npm install -g azure-functions-core-tools@4 --unsafe-perm true
     ```
   - Or download from [Azure Functions Core Tools Documentation](https://learn.microsoft.com/en-us/azure/azure-functions/functions-run-local#v4).
3. **.NET 6 SDK or Later**
   - Install from [.NET SDK Downloads](https://dotnet.microsoft.com/en-us/download/dotnet/6.0).
4. **Azure Storage Emulator or Azurite** (Optional for local testing)
   - Install Azurite via npm:
     ```bash
     npm install -g azurite
     ```
   - Alternatively, use the Azurite extension in VS Code.

### **2. Clone the Repository**
Clone the repository to your local machine:
```bash
git clone https://github.com/tig/blobuploader-imageprocessor.git
cd blobuploader-imageprocessor
```

---

## **Set Up the Project**

### **1. Install Dependencies**
Run the following command to restore dependencies:
```bash
dotnet restore
```

---

### **2. Add Configuration**
Create a `local.settings.json` file in the root directory to store local settings. Add the following content:

```json
{
  "IsEncrypted": false,
  "Values": {
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "KeyVaultUri": "https://mye28.vault.azure.net/"
  }
}
```

---

## **Run the Project Locally**

### **1. Start Azurite (Optional)**
If you want to test Azure Blob Storage locally, start Azurite in a terminal:
```bash
azurite
```

### **2. Start the Azure Function**
Run the following command to start the Azure Function:
```bash
func start
```

The function will be available at:
```
http://localhost:7071/api/ProcessImage
```

---

## **Test the Function**

### **1. Using a REST Client**
You can test the function using a REST client like Postman or cURL. Here's a sample `POST` request:

**Endpoint:**
```
http://localhost:7071/api/ProcessImage
```

**Body (JSON):**
```json
{
  "ImageBase64": "<Base64-Encoded Image>",
  "FileName": "test-image",
  "Extension": "jpg",
  "OriginalWidth": 3840,
  "OriginalHeight": 2160,
  "SizedWidth": 1920,
  "SizedHeight": 1080,
  "ThumbnailWidth": 300,
  "ThumbnailHeight": 300,
  "BlobConnectionString": "<Your Blob Connection String>",
  "BlobContainer": "test-container"
}
```

---

## **Test with the Console App**
A console test app is included in the repository for convenience.

1. Navigate to the test app directory:
   ```bash
   cd tests/ImageProcessorTest
   ```
2. Update the `Program.cs` file with the correct Azure Function URL and Blob Storage connection string.
3. Run the test app:
   ```bash
   dotnet run
   ```

---

## **Deploying to Azure**
If you have GitHub Actions configured for Continuous Deployment:

1. Push your changes to the GitHub repository:
   ```bash
   git add .
   git commit -m "Your commit message"
   git push
   ```

2. The function will be built and deployed to Azure automatically.

---

## **Debugging in VS Code**

1. Open the repository in Visual Studio Code:
   ```bash
   code .
   ```

2. Go to the **Run and Debug** panel (`Ctrl+Shift+D`).

3. Select `Attach to .NET Functions` or `Run Azure Functions` and start debugging.

---

## **Notes**

- Ensure that your Azure Function App is configured with the same connection strings as the `local.settings.json` file.
- Use the `BlobStorageConnectionString` in the Azure Function App's configuration settings when deploying.

---

Let me know if additional details are required!