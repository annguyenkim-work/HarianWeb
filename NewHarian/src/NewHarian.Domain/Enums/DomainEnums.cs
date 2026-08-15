namespace NewHarian.Domain.Enums;

public enum OrderStatus
{
    PendingPayment = 0,
    Confirmed = 1,
    Processing = 2,
    Shipped = 3,
    Delivered = 4,
    Cancelled = 5,
    Refunded = 6,
    AwaitingConfirmation = 7
}

public enum PaymentStatus
{
    Pending = 0,
    Paid = 1,
    Failed = 2,
    Refunded = 3
}

public enum PaymentMethod
{
    BankTransfer = 0,
    COD = 1,
    OnlineGateway = 2,
    /// <summary>Staff-recorded paid (store / marketplace) — Phase 4 manual &amp; import.</summary>
    Offline = 3
}

/// <summary>Where the order originated. Hard-coded for Phase 4.</summary>
public enum OrderSource
{
    Website = 0,
    Store = 1,
    Shopee = 2,
    TikTok = 3
}

public enum InquiryStatus
{
    New = 0,
    InProgress = 1,
    Resolved = 2,
    Closed = 3
}

public enum ProductStatus
{
    Draft = 0,
    Published = 1,
    Archived = 2
}

/// <summary>Discriminates guest/admin hubs after Products and Services were split into separate tables.</summary>
public enum CatalogKind
{
    Product = 0,
    Service = 1
}

public enum ServiceBookingStatus
{
    New = 0,
    Confirmed = 1,
    Completed = 2,
    Cancelled = 3
}

public enum ContentBlockType
{
    TextWithImage = 0,
    BulletList = 1,
    CtaButton = 2,
    ImageGallery = 3,
    DataTable = 4,
    ZigzagFeature = 5,
    RichText = 6
}

public enum ApplicationStatus
{
    New = 0,
    Reviewing = 1,
    Accepted = 2,
    Rejected = 3,
    Closed = 4
}

public enum ApplicationType
{
    Application = 0,
    Inquiry = 1
}

public enum PostKind
{
    News = 0,
    Job = 1
}

public enum DealerStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}
