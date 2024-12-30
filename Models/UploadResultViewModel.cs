using Newtonsoft.Json;

namespace GuardID.Models
{  
    public class UploadResultViewModel
    {        
        public string? SelfieImage { get; set; }
        public string? FrontIdImage { get; set; }
        public string? SelfieFaceImage { get; set; }
        public string? FrontIdFaceImage { get; set; }
        public string? FrontIdOCRImage { get; set; }
        public string? BackIdImage { get; set; }
        public BarcodeDataViewModel BarcodeText { get; set; }
        public string? BarcodeTextstr { get; set; }
        public ImageMetadata Metadata { get; set; }
        public List<string> ExtractedLinesFrontID { get; set; }
        public string? StringLines { get; set; }
        public double TextSimilarity { get; set; }
        public NominatimResponse Location { get; set; }
        public IpInfo IpLocation { get; set; }      
        public double Distance { get; set; }
        public string? FaceMatch { get; set; }
        public double FaceScore { get; set; }
        public double Threshold { get; set; }
        public double OverallScore { get; set; }
        public double DistancePercentageScore { get; set; }
        public double FaceScorePercentage { get; set; }                

        public UploadResultViewModel()
        {
            this.BarcodeText = new BarcodeDataViewModel();
            this.Metadata = new ImageMetadata();
            this.ExtractedLinesFrontID = new List<string>();
            this.Location = new NominatimResponse();
            this.IpLocation = new IpInfo();           
        }
    }
    public class BarcodeDataViewModel
    {
        public string CustomerID { get; set; }
        public string LastName { get; set; }
        public string FirstName { get; set; }
        public string MiddleName { get; set; }
        public DateTime DateOfBirth { get; set; }
        public string Sex { get; set; }
        public string Height { get; set; }
        public string Street { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string PostalCode { get; set; }
        public string Country { get; set; }
        public DateTime IssueDate { get; set; }
        public DateTime ExpiryDate { get; set; }
        public string DocumentDiscriminator { get; set; }
        public string IssuerIdentificationNumber { get; set; }
    }
    public class ImageMetadata
    {
        public string CameraMake { get; set; }
        public string CameraModel { get; set; }
        public string ModifyDate { get; set; }
        public string DateTimeOriginal { get; set; }
        public string FocalLength { get; set; }
        public Double GPSLatitude { get; set; }
        public Double GPSLongitude { get; set; }
        public string Aperture { get; set; }       
        public string ShutterSpeed { get; set; }
        public string Flash { get; set; }       
    }
    public class NominatimResponse
    {        

        [JsonProperty("display_name")]
        public string DisplayName { get; set; }
        [JsonProperty("address")]
        public Address Address { get; set; }
        
    }

    public class Address
    {
        [JsonProperty("county")]
        public string County { get; set; }              
        [JsonProperty("state")]
        public string State { get; set; }      
        [JsonProperty("postcode")]
        public string Postcode { get; set; }
        [JsonProperty("country")]
        public string Country { get; set; }
        [JsonProperty("country_code")]
        public string CountryCode { get; set; }
    }
    public class IpInfo
    {
        public string Query { get; set; }
        public string Status { get; set; }
        public string Country { get; set; }
        public string CountryCode { get; set; }
        public string Region { get; set; }
        public string RegionName { get; set; }
        public string City { get; set; }
        public string Zip { get; set; }
        public float Lat { get; set; }
        public float Lon { get; set; }
        public string Timezone { get; set; }
        public string Isp { get; set; }
        public string Org { get; set; }
        public string As { get; set; }
    }        
}
