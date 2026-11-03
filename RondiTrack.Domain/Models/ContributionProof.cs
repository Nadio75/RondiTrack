namespace RondiTrack.Domain;

public sealed class ContributionProof
{
    private ContributionProof() { }

    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid ContributionId { get; private set; }

    public Contribution Contribution { get; private set; } = null!;

    public Guid UploadedByUserId { get; private set; }

    public string FileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public byte[] FileData { get; private set; } = [];

    public DateTime UploadedAtUtc { get; private set; }

    public ContributionProof(
        Guid contributionId,
        Guid uploadedByUserId,
        string fileName,
        string contentType,
        byte[] fileData,
        DateTime uploadedAtUtc)
    {
        if (contributionId == Guid.Empty)
            throw new ArgumentException("A valid contribution is required.");

        if (uploadedByUserId == Guid.Empty)
            throw new ArgumentException("A valid uploader is required.");

        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("A filename is required.");

        if (string.IsNullOrWhiteSpace(contentType))
            throw new ArgumentException("A content type is required.");

        if (fileData is null || fileData.Length == 0)
            throw new ArgumentException("The proof file cannot be empty.");

        if (uploadedAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Upload time must be UTC.");

        ContributionId = contributionId;
        UploadedByUserId = uploadedByUserId;
        FileName = fileName;
        ContentType = contentType;
        FileData = (byte[])fileData.Clone();
        UploadedAtUtc = uploadedAtUtc;
    }

    public void Replace(Guid uploadedByUserId, string fileName, string contentType, byte[] fileData, DateTime uploadedAtUtc)
    {
        if (uploadedByUserId == Guid.Empty) throw new ArgumentException("A valid uploader is required.");
        if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("A filename is required.");
        if (string.IsNullOrWhiteSpace(contentType)) throw new ArgumentException("A content type is required.");
        if (fileData is null || fileData.Length == 0) throw new ArgumentException("The proof file cannot be empty.");
        if (uploadedAtUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Upload time must be UTC.");
        UploadedByUserId = uploadedByUserId;
        FileName = fileName;
        ContentType = contentType;
        FileData = (byte[])fileData.Clone();
        UploadedAtUtc = uploadedAtUtc;
    }
}
