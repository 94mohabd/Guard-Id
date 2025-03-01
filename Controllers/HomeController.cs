using Microsoft.AspNetCore.Mvc;
using GuardID.Models;
using Inlite.ClearImageNet;
using System.Drawing.Imaging;
using System.Globalization;
using Newtonsoft.Json;
using System.Xml.Linq;
using System.Text;
using System.Net;
using GeoCoordinatePortable;
using DlibDotNet;
using Google.Cloud.Vision.V1;
using DlibDotNet.Extensions;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using System.Runtime.InteropServices;

namespace GuardID.Controllers
{
    public class HomeController : Controller
    {
        public HomeController()
        {
        }
        [Route("/")]
        public IActionResult Index()
        {
            ViewBag.OS = @RuntimeInformation.OSDescription;
            return View();
        }
        [Route("/Result")]
        public IActionResult Result()
        {
            return View();
        }
        [HttpGet]
        [Route("/UploadID")]
        public virtual ActionResult UploadID()
        {
            return View();
        }
        [HttpPost]
        public IActionResult DetectFaceinImage(IFormFile imageFile)
        {
            try
            {
                var imageFilePath = SaveToTempFile(imageFile);

                var imageFaces = DetectFacesAndConvertToBase64(imageFilePath);
                //check if any faces detected
                if (!imageFaces.Any())
                {
                    return Json(new { success = false, message = "Failed to detect face " });
                }
                else
                {
                    return Json(new { success = true, message = "Face is detected" });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An error occurred: " + ex.Message });
            }
        }
        [HttpPost]
        public IActionResult DetectBarCodeinImage(IFormFile imageFile)
        {
            try
            {
                var barcodeText = ExtractBarcodeText(imageFile);
                var parseBarcodeText = ParseBarcodeText(barcodeText);
                var xmlTable = parseBarcodeText.xmlTable;
                // check if any PDF417 barcode detected from the front Id
                if (string.IsNullOrEmpty(barcodeText))
                {
                    return Json(new { success = false, message = "Failed to detect PDF417 barcode" });
                }
                if (xmlTable == null)
                {
                    return Json(new { success = false, message = "Please provide a valid ID!" });
                }
                else
                {
                    return Json(new { success = true, message = "PDF417 barcode is detected" });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An error occurred: " + ex.Message });
            }
        }
        [HttpPost]
        public IActionResult DetectTextinImage(IFormFile imageFile)
        {
            var imageFilePath = SaveToTempFile(imageFile);
            var extractedLinesFrontID = ExtractTextFromImage(imageFilePath);
            // check if any Text detected from the front Id image
            if (extractedLinesFrontID != null && !extractedLinesFrontID.Any())
            {
                return Json(new { success = false, message = "Failed to detect Text" });
            }
            else
            {
                return Json(new { success = true, message = "Text is detected", Text = extractedLinesFrontID });
            }
        }
        [HttpPost]
        [Route("/UploadID")]
        public virtual async Task<ActionResult> UploadID(Guid Id, IFormFile frontId, IFormFile backId, IFormFile selfie)
        {
            if (selfie == null || selfie.Length == 0)
            {
                ViewBag.Error = "Selfie image is required.";
                return View(nameof(UploadID));
            }

            if (frontId == null || frontId.Length == 0)
            {
                ViewBag.Error = "Front ID image is required.";
                return View(nameof(UploadID));
            }

            if (backId == null || backId.Length == 0)
            {
                ViewBag.Error = "Back ID image is required.";
                return View(nameof(UploadID));
            }
            // save images to a temp file          
            var selfieFilePath = SaveToTempFile(selfie);
            var frontIdFilePath = SaveToTempFile(frontId);

            var selfieFaces = DetectFacesAndConvertToBase64(selfieFilePath);

            var frontIdFaces = DetectFacesAndConvertToBase64(frontIdFilePath);
            // check if any faces detected
            if (!frontIdFaces.Any() && !selfieFaces.Any())
            {
                ViewBag.Error = "No face detected in the front ID image and the selfie image.";
                return View(nameof(UploadID));
            }
            if (!selfieFaces.Any())
            {
                ViewBag.Error = "No face detected in the selfie image.";
                return View(nameof(UploadID));
            }
            if (!frontIdFaces.Any())
            {
                ViewBag.Error = "No face detected in the front ID image.";
                return View(nameof(UploadID));
            }

            // Convert Images to Base64Strings to show to frontEnd           
            string frontIdImageBase64String = ConvertToBase64String(frontId);
            string backIdImageBase64String = ConvertToBase64String(backId);
            string selfieImageBase64String = ConvertToBase64String(selfie);

            var selfieFaceImageBase64String = selfieFaces.FirstOrDefault();
            var frontIdFaceImageBase64String = frontIdFaces.FirstOrDefault();

            // OCR processing to extract text from the front Id image                        
            var extractedLinesFrontID = ExtractTextFromImage(frontIdFilePath);
            // check if any Text detected from the front Id image
            if (extractedLinesFrontID != null && !extractedLinesFrontID.Any())
            {
                ViewBag.Error = "No Text detected in Front Id!";
                return View(nameof(UploadID));
            }

            // get the lines extracted as a string
            var stringLines = string.Join(" ", extractedLinesFrontID);

            // Extracts text from a Pdf417 barcode in the uploaded file.
            var barcodeText = ExtractBarcodeText(backId);
            var parseBarcodeText = ParseBarcodeText(barcodeText);
            var xmlTable = parseBarcodeText.xmlTable;
            var barcodeTextStr = parseBarcodeText.barcodeText;
            // compare the extracted text from barcode with detected text from the front Id image
            var textSimilarity = CompareLists(ParseBarcodeTextToList(barcodeText), extractedLinesFrontID);
            // check if any PDF417 barcode detected from the front Id
            if (string.IsNullOrEmpty(barcodeText))
            {
                ViewBag.Error = "No PDF417 barcode detected in Back Id!";
                return View(nameof(UploadID));
            }
            if (xmlTable == null)
            {
                ViewBag.Error = "Please provide a valid ID!";
                return View(nameof(UploadID));
            }

            // Extracts EXIF metadata from an image file and maps it to an ImageMetadata object.
            var exifInfo = GetExifInfo(selfieFilePath);
            // Retrieves location data from the 3rd party Nominatim API based on latitude and longitude.
            var location = await GetLocationAsync(exifInfo.GPSLatitude, exifInfo.GPSLongitude);

            string ipAddress = HttpContext.Connection.RemoteIpAddress.ToString();

            // Retrieves IP Latitude and Longitude from a 3rd party ip-api API based on the provided IP address.
            var addressFromIP = GetAddressFromIP(ipAddress);
            var lat = addressFromIP.Lat;
            var lon = addressFromIP.Lon;

            // Create a GeoCoordinate object for the selfie location using latitude and longitude from EXIF data
            var distance = double.NaN;
            if (exifInfo.GPSLatitude >= -90 && exifInfo.GPSLatitude <= 90 && exifInfo.GPSLongitude >= -180 && exifInfo.GPSLongitude <= 180)
            {
                var selfieCoord = new GeoCoordinate(exifInfo.GPSLatitude, exifInfo.GPSLongitude);

                // Create a GeoCoordinate object for the IP location using provided latitude and longitude
                var ipCoord = new GeoCoordinate(lat, lon);

                // Calculate the distance between the selfie location and the IP location in miles
                // Convert the distance from meters to miles (1 mile = 1609.344 meters)
                // Round the result to 2 decimal places
                distance = Math.Round(selfieCoord.GetDistanceTo(ipCoord) / 1609.344, 2);
            }

            // Define the threshold for face matching.          
            var threshold = 0.6;

            // Call the CompareFaces method with the file paths for the ID image and the selfie, and the defined threshold. The method returns a tuple with faceScore and faceMatch.
            var compareFaces = CompareFaces(frontIdFilePath, selfieFilePath, threshold);
            //var response = CompareFaces(SaveToTempFile(frontId, 500, 500), SaveToTempFile(selfie, 100, 500), threshold);

            // Extract the face score and faceMatch from the response 
            var faceScore = compareFaces.faceScore;
            var faceMatch = compareFaces.faceMatch;

            // scoring System
            var faceScorePercentage = faceScore > threshold ? 0 : 100 - ((faceScore / 0.5) * 5);
            var distancePercentageScore = 0.0;
            if (!double.IsNaN(distance))
            {
                distancePercentageScore = distance > 100 ? 0 : (1 - (distance / 500)) * 100;
            }
            var overallScore = Math.Round((faceScorePercentage * 0.45) + (textSimilarity * 0.45) + (distancePercentageScore * 0.10), 2);

            var result = new UploadResultViewModel
            {
                FrontIdImage = frontIdImageBase64String,
                BackIdImage = backIdImageBase64String,
                SelfieImage = selfieImageBase64String,
                FrontIdFaceImage = frontIdFaceImageBase64String,
                SelfieFaceImage = selfieFaceImageBase64String,
                ExtractedLinesFrontID = extractedLinesFrontID,
                StringLines = stringLines,
                BarcodeText = xmlTable,
                BarcodeTextstr = barcodeTextStr,
                Metadata = exifInfo,
                TextSimilarity = textSimilarity,
                Location = location,
                IpLocation = addressFromIP,
                Distance = distance,
                FaceMatch = faceMatch,
                FaceScore = faceScore,
                Threshold = threshold,
                OverallScore = overallScore,
                DistancePercentageScore = distancePercentageScore,
                FaceScorePercentage = faceScorePercentage,
            };
            return View(nameof(Result), result);
        }
        private static List<string> DetectFacesAndConvertToBase64(string imagePath)
        {
            string shapePredictorPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "shape_predictor_68_face_landmarks.dat");

            using (var detector = Dlib.GetFrontalFaceDetector())
            using (var shapePredictor = ShapePredictor.Deserialize(shapePredictorPath))
            using (var image = Dlib.LoadImage<RgbPixel>(imagePath))
            {
                var faces = detector.Operator(image);
                var faceBase64Strings = new List<string>();

                foreach (var face in faces)
                {
                    var shape = shapePredictor.Detect(image, face);
                    var faceChipDetails = Dlib.GetFaceChipDetails(shape);

                    using (var faceImage = Dlib.ExtractImageChip<RgbPixel>(image, faceChipDetails))
                    {
                        // Convert Dlib image to ImageSharp image
                        var imageSharp = ConvertDlibToImageSharp(faceImage);

                        // Convert ImageSharp to Base64
                        using (var ms = new MemoryStream())
                        {
                            imageSharp.SaveAsPng(ms);
                            faceBase64Strings.Add(Convert.ToBase64String(ms.ToArray()));
                        }
                    }
                }

                return faceBase64Strings;
            }
        }
        private static SixLabors.ImageSharp.Image<Rgb24> ConvertDlibToImageSharp(Array2D<RgbPixel> dlibImage)
        {
            int width = dlibImage.Columns;
            int height = dlibImage.Rows;
            var imageSharp = new SixLabors.ImageSharp.Image<Rgb24>(width, height);

            for (int y = 0; y < height; y++)
            {
                var row = dlibImage[y]; // GetRow instead of direct indexing

                for (int x = 0; x < width; x++)
                {
                    RgbPixel pixel = row[x]; // Now access pixel correctly
                    imageSharp[x, y] = new Rgb24(pixel.Red, pixel.Green, pixel.Blue);
                }
            }

            return imageSharp;
        }

        private string ConvertToBase64String(IFormFile imageFile)
        {
            string base64String = string.Empty;

            using (var stream = new MemoryStream())
            {
                // Copy the image file to the memory stream
                imageFile.CopyTo(stream);

                // Convert the memory stream to a byte array
                byte[] imageData = stream.ToArray();

                // Convert the byte array to a Base64 string
                base64String = Convert.ToBase64String(imageData);
            }

            return base64String;
        }
        private string SaveToTempFile(IFormFile file)
        {
            // Generate a temporary file path
            string tempFilePath = Path.GetTempFileName();

            // Save the uploaded file to the temporary file
            using (var stream = new FileStream(tempFilePath, FileMode.Create))
            {
                file.CopyTo(stream);
            }

            // Return the path to the temporary file
            return tempFilePath;
        }
        private (double faceScore, string faceMatch) CompareFaces(string frontIdimageFilePath, string selfieimageFilePath, double threshold)
        {
            string faceMatch = string.Empty;
            double faceScore = 0.0;
            string shapePredictorPath = Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Resources", "shape_predictor_68_face_landmarks.dat");
            string dlibFacePath = Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Resources", "dlib_face_recognition_resnet_model_v1.dat");

            try
            {
                // Load models
                using (var detector = Dlib.GetFrontalFaceDetector())
                using (var sp = ShapePredictor.Deserialize(shapePredictorPath))
                using (var net = DlibDotNet.Dnn.LossMetric.Deserialize(dlibFacePath))
                {
                    // Load images
                    using (var frontId = Dlib.LoadImageAsMatrix<RgbPixel>(frontIdimageFilePath))
                    using (var selfie = Dlib.LoadImageAsMatrix<RgbPixel>(selfieimageFilePath))
                    {
                        var frontIdFace = DetectAndAlignFace(detector, sp, frontId);
                        var selfieFace = DetectAndAlignFace(detector, sp, selfie);

                        if (frontIdFace == null || selfieFace == null)
                        {
                            return (faceScore, faceMatch);
                        }

                        var faceDescriptor1 = net.Operator(new[] { frontIdFace }).FirstOrDefault();
                        var faceDescriptor2 = net.Operator(new[] { selfieFace }).FirstOrDefault();

                        if (faceDescriptor1 == null || faceDescriptor2 == null)
                        {
                            return (faceScore, faceMatch);
                        }

                        // Compare face descriptors
                        var diff = faceDescriptor1 - faceDescriptor2;
                        var faceDistance = Dlib.Length(diff);
                        faceScore = Math.Round(faceDistance, 2);

                        // Determine if faces match based on the threshold
                        faceMatch = faceDistance < threshold ? "The faces are from the same person." : "The faces are from different people.";
                    }
                }
            }
            catch (Exception ex)
            {
                return (faceScore, ex.Message);
                // Log the exception (optional)
            }

            return (faceScore, faceMatch);
        }
        private string ExtractBarcodeText(IFormFile backId)
        {
            var barcodeText = string.Empty;

            // Use a more reliable temporary file location
            string tempDir = Path.Combine(Directory.GetCurrentDirectory(), "temp");
            Directory.CreateDirectory(tempDir); // Ensure temp directory exists

            string backIdFilePath = Path.Combine(tempDir, Path.GetRandomFileName() + ".jpg");

            try
            {
                // Save file to temp location
                using (var fileStream = new FileStream(backIdFilePath, FileMode.Create, FileAccess.Write))
                {
                    backId.CopyTo(fileStream);
                }

                // Open the file and read barcode
                using (var fileStream = new FileStream(backIdFilePath, FileMode.Open, FileAccess.Read))
                using (BarcodeReader reader = new BarcodeReader())
                {
                    // Enable PDF417 and Driver License scanning
                    reader.Pdf417 = true;
                    reader.DrvLicID = true;

                    // Read barcodes
                    Barcode[] barcodes = reader.Read(fileStream);
                    if (barcodes != null && barcodes.Length > 0)
                    {
                        var barcode = barcodes.FirstOrDefault();
                        if (barcode.Type.ToString() == "Pdf417")
                        {
                            barcodeText = barcode.Text;
                        }
                        else
                        {
                            barcodeText = barcode.Type.ToString(); // If it's not PDF417, return type
                        }
                    }
                    else
                    {
                        barcodeText = "No barcode detected";
                    }
                }
            }
            catch (FileNotFoundException fnfEx)
            {
                Console.WriteLine($"File not found: {fnfEx.Message}");
            }
            catch (UnauthorizedAccessException uaEx)
            {
                Console.WriteLine($"Unauthorized access: {uaEx.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading barcode: {ex.Message}");
            }
            finally
            {
                // Clean up temp file to avoid leaks
                if (System.IO.File.Exists(backIdFilePath))
                {
                    System.IO.File.Delete(backIdFilePath);
                }
            }

            return barcodeText;
        }

        private List<string> ExtractTextFromImage(string idFilePath)
        {
            List<string> lines = new List<string>();
            var OCRImage = string.Empty;

            string credentialPath = Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Resources", "GoogleCloudVision.json");

            if (!System.IO.File.Exists(credentialPath))
            {
                string base64Json = Environment.GetEnvironmentVariable("GOOGLE_CLOUD_VISION");

                if (!string.IsNullOrEmpty(base64Json))
                {
                    // Decode the base64 string and write it to a temporary file
                    string tempCredentialPath = Path.GetTempFileName();
                    System.IO.File.WriteAllBytes(tempCredentialPath, Convert.FromBase64String(base64Json));

                    // Set the environment variable to the path of the temporary credentials file
                    Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", tempCredentialPath);
                }
                else
                {
                    throw new FileNotFoundException("Google Cloud Vision credentials base64 not found in environment variables.");
                }
            }
            else
            {
                // If the file exists, just set the environment variable to the file path
                Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", credentialPath);
            }
            // Convert processed image to base64 string
            OCRImage = ConvertJpgToBase64(idFilePath);

            // Perform OCR using Google Cloud Vision
            var client = ImageAnnotatorClient.Create();
            var googleImage = Google.Cloud.Vision.V1.Image.FromFile(idFilePath);

            var response = client.DetectText(googleImage);
            var extractedText = response.FirstOrDefault()?.Description ?? string.Empty;

            // Split the extracted text into lines
            string[] linesArray = extractedText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            lines = new List<string>(linesArray).Where(text => text.Any(char.IsLetterOrDigit)).ToList();

            return lines;
        }
        private static string ConvertJpgToBase64(string filePath)
        {
            byte[] imageBytes = System.IO.File.ReadAllBytes(filePath);
            return Convert.ToBase64String(imageBytes);
        }
        private static DlibDotNet.Matrix<RgbPixel> DetectAndAlignFace(FrontalFaceDetector detector, ShapePredictor sp, DlibDotNet.Matrix<RgbPixel> img)
        {
            var faces = detector.Operator(img);
            if (faces.Length == 0)
                return null;

            var shape = sp.Detect(img, faces.First());
            var faceChipDetail = Dlib.GetFaceChipDetails(shape, 150, 0.25);
            return Dlib.ExtractImageChip<RgbPixel>(img, faceChipDetail);
        }
        private IpInfo GetAddressFromIP(string ipAddress)
        {
            string apiUrl = $"http://ip-api.com/json/{ipAddress}";
            using (WebClient client = new WebClient())
            {
                try
                {
                    string responseBody = client.DownloadString(apiUrl);
                    IpInfo ipInfo = JsonConvert.DeserializeObject<IpInfo>(responseBody);
                    return ipInfo; // Return the deserialized object
                }
                catch (WebException ex)
                {
                    // Handle any WebException here, e.g., timeout or network error
                    return null;
                }
            }
        }
        private double CompareLists(List<string> list1, List<string> list2)
        {
            if (list1 == null || list2 == null)
            {
                throw new ArgumentException("Input lists cannot be null.");
            }

            int totalWordsChecked = list1.Count;
            if (totalWordsChecked == 0)
            {
                throw new ArgumentException("Input lists cannot be empty.");
            }
            string list2String = string.Join(" ", list2).Replace("-", "").Replace(".", "").Replace("'", "").Replace("\"", "");
            // Convert list2 into a HashSet for faster lookups
            HashSet<string> set2 = new HashSet<string>(list2);

            int score = 0;
            var matches = new List<string>();
            var Nomatches = new List<string>();
            foreach (string item in list1)
            {
                if (list2String.Contains(item.Replace("- ", "").Replace("'", "").Replace("\"", "")))
                {
                    matches.Add(item);
                    score++;
                }
                else
                {
                    Nomatches.Add(item);
                }
            }
            ViewBag.matches = matches;
            ViewBag.Nomatches = Nomatches;
            ViewBag.totalWordsChecked = totalWordsChecked;
            ViewBag.score = score;
            // Calculate the fraction of items in list1 that are found in list2
            double scoreFraction = (double)score / totalWordsChecked;
            return Math.Round(scoreFraction * 100, 2); // Return percentage score
        }
        private List<string> ParseBarcodeTextToList(string barcodeText)
        {
            List<string> values = new List<string>();

            try
            {
                var doc = XDocument.Parse(barcodeText);

                values.Add(doc.Descendants("element").FirstOrDefault(e => e.Attribute("id")?.Value == "DAQ")?.Value.Substring(1));
                values.Add(doc.Descendants("element").FirstOrDefault(e => e.Attribute("id")?.Value == "DCS")?.Value);
                values.Add(doc.Descendants("element").FirstOrDefault(e => e.Attribute("id")?.Value == "DAC")?.Value);
                values.Add(doc.Descendants("element").FirstOrDefault(e => e.Attribute("id")?.Value == "DAD")?.Value);
                values.Add(DateTime.ParseExact(doc.Descendants("element").FirstOrDefault(e => e.Attribute("id")?.Value == "DBB")?.Value, "MMddyyyy", null).ToShortDateString());
                values.Add(doc.Descendants("element").FirstOrDefault(e => e.Attribute("id")?.Value == "DBC")?.Value == "1" ? "M" : "F");
                values.Add(doc.Descendants("height").FirstOrDefault(e => e.Attribute("e")?.Value == "DAU")?.Value.Replace("'", "").Replace("\"", ""));
                values.Add(doc.Descendants("element").FirstOrDefault(e => e.Attribute("id")?.Value == "DAG")?.Value);
                values.Add(doc.Descendants("element").FirstOrDefault(e => e.Attribute("id")?.Value == "DAI")?.Value);
                values.Add(doc.Descendants("element").FirstOrDefault(e => e.Attribute("id")?.Value == "DAJ")?.Value);
                values.Add(doc.Descendants("element").FirstOrDefault(e => e.Attribute("id")?.Value == "DAK")?.Value);
                values.Add(DateTime.ParseExact(doc.Descendants("element").FirstOrDefault(e => e.Attribute("id")?.Value == "DBD")?.Value, "MMddyyyy", null).ToShortDateString());
                values.Add(DateTime.ParseExact(doc.Descendants("element").FirstOrDefault(e => e.Attribute("id")?.Value == "DBA")?.Value, "MMddyyyy", null).ToShortDateString());
                values.Add(doc.Descendants("element").FirstOrDefault(e => e.Attribute("id")?.Value == "DCF")?.Value);
                //values.Add(doc.Descendants("element").FirstOrDefault(e => e.Attribute("id")?.Value == "DCG")?.Value);
                //values.Add(doc.Descendants("issuer").FirstOrDefault(e => e.Attribute("name")?.Value == "Issuer Identification Number")?.Value);

                return values;
            }
            catch (Exception ex)
            {
                return barcodeText.Split(',').ToList();
            }
        }
        private (BarcodeDataViewModel xmlTable, string barcodeText) ParseBarcodeText(string barcodeText)
        {
            var model = new BarcodeDataViewModel();
            try
            {
                var doc = XDocument.Parse(barcodeText);
                model.CustomerID = doc.Descendants("element").FirstOrDefault(e => e.Attribute("id")?.Value == "DAQ")?.Value.Substring(1);
                model.LastName = doc.Descendants("element").FirstOrDefault(e => e.Attribute("id")?.Value == "DCS")?.Value;
                model.FirstName = doc.Descendants("element").FirstOrDefault(e => e.Attribute("id")?.Value == "DAC")?.Value;
                model.MiddleName = doc.Descendants("element").FirstOrDefault(e => e.Attribute("id")?.Value == "DAD")?.Value;
                model.DateOfBirth = DateTime.ParseExact(doc.Descendants("element").FirstOrDefault(e => e.Attribute("id")?.Value == "DBB")?.Value, "MMddyyyy", null);
                model.Sex = doc.Descendants("element").FirstOrDefault(e => e.Attribute("id")?.Value == "DBC")?.Value == "1" ? "M" : "F";
                model.Height = doc.Descendants("height").FirstOrDefault(e => e.Attribute("e")?.Value == "DAU")?.Value.Replace("'", "").Replace("\"", "");
                model.Street = doc.Descendants("element").FirstOrDefault(e => e.Attribute("id")?.Value == "DAG")?.Value;
                model.City = doc.Descendants("element").FirstOrDefault(e => e.Attribute("id")?.Value == "DAI")?.Value;
                model.State = doc.Descendants("element").FirstOrDefault(e => e.Attribute("id")?.Value == "DAJ")?.Value;
                model.PostalCode = doc.Descendants("element").FirstOrDefault(e => e.Attribute("id")?.Value == "DAK")?.Value;
                model.IssueDate = DateTime.ParseExact(doc.Descendants("element").FirstOrDefault(e => e.Attribute("id")?.Value == "DBD")?.Value, "MMddyyyy", null);
                model.ExpiryDate = DateTime.ParseExact(doc.Descendants("element").FirstOrDefault(e => e.Attribute("id")?.Value == "DBA")?.Value, "MMddyyyy", null);
                model.DocumentDiscriminator = doc.Descendants("element").FirstOrDefault(e => e.Attribute("id")?.Value == "DCF")?.Value;
                model.IssuerIdentificationNumber = doc.Descendants("issuer").FirstOrDefault(e => e.Attribute("name")?.Value == "Issuer Identification Number")?.Value;
                model.Country = doc.Descendants("element").FirstOrDefault(e => e.Attribute("id")?.Value == "DCG")?.Value;
                return (model, "");
            }
            catch (Exception ex)
            {
                return (null, barcodeText);
            }

        }
        private async Task<NominatimResponse> GetLocationAsync(double latitude, double longitude)
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "YourAppName/1.0 (your.email@example.com)");
                var url = $"https://nominatim.openstreetmap.org/reverse?format=jsonv2&lat={latitude}&lon={longitude}";

                var response = await client.GetAsync(url);

                // Check if the response is successful
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    if (json.Contains("error"))
                    {
                        return new NominatimResponse
                        {
                            DisplayName = "Not Available",
                            Address = new Address
                            {
                                County = "Not Available",
                                State = "Not Available",
                                Postcode = "Not Available",
                                Country = "Not Available",
                                CountryCode = "Not Available"

                            }
                        };
                    }
                    else
                    {
                        var nominatimResponse = JsonConvert.DeserializeObject<NominatimResponse>(json);
                        if (nominatimResponse != null)
                        {
                            return nominatimResponse;
                        }
                    }
                }
            }
            return new NominatimResponse
            {
                DisplayName = "Not Available",
                Address = new Address
                {
                    County = "Not Available",
                    State = "Not Available",
                    Postcode = "Not Available",
                    Country = "Not Available",
                    CountryCode = "Not Available"

                }
            };
        }
        private ImageMetadata GetExifInfo(string filePath)
        {
            double latitude = double.NaN;
            double longitude = double.NaN;
            char latitudeRef = 'N';
            char longitudeRef = 'E';

            var viewModel = new ImageMetadata();
            using (var image = System.Drawing.Image.FromFile(filePath))
            {
                foreach (PropertyItem propertyItem in image.PropertyItems)
                {
                    switch (propertyItem.Id)
                    {
                        case 0x010F: // Make
                            viewModel.CameraMake = Encoding.ASCII.GetString(propertyItem.Value).Trim('\0');
                            break;
                        case 0x0110: // Model
                            viewModel.CameraModel = Encoding.ASCII.GetString(propertyItem.Value).Trim('\0');
                            break;
                        case 0x0132: // Modify Date
                            viewModel.ModifyDate = Encoding.ASCII.GetString(propertyItem.Value).Trim('\0');
                            break;
                        case 0x9003: // Date/Time Original
                            viewModel.DateTimeOriginal = Encoding.ASCII.GetString(propertyItem.Value).Trim('\0');
                            break;
                        case 0x920A: // Focal Length
                            viewModel.FocalLength = (BitConverter.ToUInt32(propertyItem.Value, 0) / (double)BitConverter.ToUInt32(propertyItem.Value, 4)).ToString(CultureInfo.InvariantCulture);
                            break;
                        case 0x0002: // GPS Latitude                            
                            latitude = GetCoordinate(propertyItem.Value);
                            break;
                        case 0x0004: // GPS Longitude                            
                            longitude = GetCoordinate(propertyItem.Value);
                            break;
                        case 0x0001: // Latitude Reference
                            latitudeRef = (char)propertyItem.Value[0];
                            break;
                        case 0x0003: // Longitude Reference
                            longitudeRef = (char)propertyItem.Value[0];
                            break;
                        case 0x829D: // Aperture
                            viewModel.Aperture = (BitConverter.ToUInt32(propertyItem.Value, 0) / (double)BitConverter.ToUInt32(propertyItem.Value, 4)).ToString(CultureInfo.InvariantCulture);
                            break;
                        case 0x9201: // Shutter Speed
                            viewModel.ShutterSpeed = (BitConverter.ToUInt32(propertyItem.Value, 0) / (double)BitConverter.ToUInt32(propertyItem.Value, 4)).ToString(CultureInfo.InvariantCulture);
                            break;
                        case 0x9209: // Flash
                            viewModel.Flash = (propertyItem.Value[0] & 0x1) != 0 ? "Flash fired" : "Flash did not fire";
                            break;
                    }
                }
            }
            if (latitudeRef == 'S')
            {
                viewModel.GPSLatitude = -latitude;
            }
            else
            {
                viewModel.GPSLatitude = latitude;
            }

            if (longitudeRef == 'W')
            {
                viewModel.GPSLongitude = -longitude;
            }
            else
            {
                viewModel.GPSLongitude = longitude;
            }
            return viewModel;
        }
        private double GetCoordinate(byte[] value)
        {
            // EXIF stores coordinates as an array of three rational numbers: degrees, minutes, seconds.
            double degrees = BitConverter.ToUInt32(value, 0) / (double)BitConverter.ToUInt32(value, 4);
            double minutes = BitConverter.ToUInt32(value, 8) / (double)BitConverter.ToUInt32(value, 12);
            double seconds = BitConverter.ToUInt32(value, 16) / (double)BitConverter.ToUInt32(value, 20);

            return degrees + (minutes / 60.0) + (seconds / 3600.0);
        }
    }
}
