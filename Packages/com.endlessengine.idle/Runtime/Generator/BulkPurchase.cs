namespace EndlessEngine.Generator
{
    /// <summary>How many copies to buy in a bulk purchase operation.</summary>
    public enum BulkPurchaseMode
    {
        /// <summary>Buy exactly 1 (same as TryPurchase).</summary>
        One = 1,

        /// <summary>Buy exactly 10, or fewer if balance/cap prevents it.</summary>
        Ten = 10,

        /// <summary>Buy as many as the current balance allows in one transaction.</summary>
        Max = 0,

        /// <summary>Buy until count reaches the target specified in TryPurchaseBulk(untilCount).</summary>
        Until = -1,
    }

    /// <summary>Result of a bulk purchase attempt.</summary>
    public readonly struct BulkPurchaseResult
    {
        /// <summary>Number of copies actually purchased (0 if failed).</summary>
        public readonly int Purchased;

        /// <summary>Total gold deducted. 0 if nothing was purchased.</summary>
        public readonly double TotalCost;

        public bool Success => Purchased > 0;

        public BulkPurchaseResult(int purchased, double totalCost)
        {
            Purchased = purchased;
            TotalCost = totalCost;
        }
    }
}
