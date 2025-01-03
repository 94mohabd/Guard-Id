# ASP.NET MVC Face and Barcode Detection Project

This is an ASP.NET MVC application that integrates multiple image processing features such as face detection, barcode extraction, text recognition, and liveness detection. The application allows users to upload images of identification documents (IDs) and selfies for comparison, and provides various analysis and scoring based on these inputs.

## Features

- **Face Detection**: Detect faces in images (selfie and ID images).
- **Barcode Extraction**: Detect and extract PDF417 barcodes from images (e.g., from the back of an ID).
- **Text Extraction**: Extract text from images using OCR (Optical Character Recognition).
- **Geolocation**: Retrieve location information from EXIF metadata and the user's IP address.
- **Face Comparison**: Compare faces between the selfie and the ID to assess similarity.
- **Distance Calculation**: Calculate the distance between the selfie location (from EXIF data) and the IP address location to generate a location-based score.
- **Scoring System**: Calculate an overall score based on face matching, text similarity, and geolocation.

## Technologies Used

- **ASP.NET MVC**: Framework for building the web application.
- **DlibDotNet**: Library for face recognition and comparison.
- **Google Cloud Vision**: Used for Optical Character Recognition (OCR).
- **GeoCoordinatePortable**: Library for geolocation and distance calculations.
- **Inlite ClearImageNet**: Barcode recognition library for extracting PDF417 barcodes.

## Setup

### Prerequisites

- .NET 8.0 or higher
- Visual Studio or any IDE that supports .NET development
- Install the required NuGet packages:  
    - `Inlite.ClearImageNet`
    - `DlibDotNet`
    - `Google.Cloud.Vision.V1`
    - `GeoCoordinatePortable`  

### Configuration

Ensure that you have API keys configured for Google Cloud Vision OCR and any necessary libraries for barcode detection.

Endpoints
- **GET /:** Displays the home page.
- **GET /UploadID:** Displays the ID upload page where users can upload images (front ID, back ID, and selfie).
- **POST /DetectFaceinImage:** Detect faces in the uploaded image file.
- **POST /DetectBarCodeinImage:** Extract barcode data from the uploaded image.
- **POST /DetectTextinImage:** Extract text data from the uploaded image.
- **POST /UploadID:** Handles the complete ID and selfie upload process, performs face detection, barcode extraction, text recognition, and geolocation, and generates a final score.

### Image Processing Workflow

- **Upload ID and Selfie:** Users upload their front ID image, back ID image, and selfie.
- **Face Detection:** Faces are detected in both the selfie and the front ID images. If no faces are detected, the upload is rejected.
- **Text Recognition (OCR):** Text is extracted from the front ID using OCR.
- **Barcode Extraction:** A PDF417 barcode is extracted from the back ID image and validated.
- **Geolocation:** The location of the selfie is determined from its EXIF metadata. The user's IP address is also used to estimate the user's location.
- **Face Comparison:** The system compares the faces in the front ID image and the selfie, calculating a similarity score.
- **Distance Calculation:** The distance between the selfie location and the IP location is calculated and factored into the final score.
- **Scoring:** A final score is generated based on face matching, text similarity, and location proximity.
