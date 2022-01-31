namespace BTCexchange.Models
{
    public partial class Order
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public long FilledQuantity { get; set; }
        public long RemainQuantity { get; set; }
        public double AvgPrice { get; set; }
        public string Status { get; set; } = null!;
        public bool Buying { get; set; }
        public long LimitPrice { get; set; }
        public string? NotifyUrl { get; set; }

        public OrderDTO ToDTO()
        {
            return new OrderDTO(Id, UserId, FilledQuantity, RemainQuantity, AvgPrice, Status, Buying, LimitPrice, NotifyUrl);
        }
    }

    public class OrderDTO
    {
        public int Id { get; }
        public int UserId { get; }
        public long FilledQuantity { get; set; }
        public long RemainQuantity { get; set; }
        public double AvgPrice { get; set; }
        public string Status { get; set; }
        public bool Buying { get; }
        public long LimitPrice { get; set; }
        public string? NotifyUrl { get; set; }

        public OrderDTO(int id, int userId, long filledQuantity, long remQuantity, double avgPrice, string status, bool buying, long limitPrice, string? notifyUrl)
        {
            Id = id;
            UserId = userId;
            FilledQuantity = filledQuantity;
            RemainQuantity = remQuantity;
            AvgPrice = avgPrice;
            Status = status;
            Buying = buying;
            LimitPrice = limitPrice;
            NotifyUrl = notifyUrl;
        }
    }

    public class MarketOrderReturn
    {
        public long quantity { get; }
        public double avgPrice { get; }

        public MarketOrderReturn(long quantity, double avgPrice)
        {
            this.quantity = quantity;
            this.avgPrice = avgPrice;
        }
    }
}
