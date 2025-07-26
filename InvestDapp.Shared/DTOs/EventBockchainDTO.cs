using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Numerics;

namespace InvestDapp.Shared.DTOs
{
    public class EvenBockchainDTO
    {
        [Nethereum.ABI.FunctionEncoding.Attributes.Event("CampaignCreated")]
        public class CampaignCreatedEventDTO : IEventDTO
        {
            [Parameter("uint256", "id", 1, true)] public BigInteger Id { get; set; }
            [Parameter("address", "owner", 2, true)] public string Owner { get; set; }
            [Parameter("string", "name", 3, false)] public string Name { get; set; }
            [Parameter("uint256", "goalAmount", 4, false)] public BigInteger GoalAmount { get; set; }
            [Parameter("uint256", "endTime", 5, false)] public BigInteger EndTime { get; set; }
        }

        [Nethereum.ABI.FunctionEncoding.Attributes.Event("investmentReceived")]
        public class InvestmentReceivedEventDTO : IEventDTO
        {
            [Parameter("uint256", "campaignId", 1, true)]
            public BigInteger CampaignId { get; set; }

            [Parameter("address", "investor", 2, true)]
            public string Investor { get; set; }

            [Parameter("uint256", "amount", 3, false)]
            public BigInteger Amount { get; set; }

            [Parameter("uint256", "currentRaisedAmount", 4, false)]
            public BigInteger CurrentRaisedAmount { get; set; }
        }

        [Nethereum.ABI.FunctionEncoding.Attributes.Event("WithdrawalRequested")]
        public class WithdrawalRequestedEventDTO : IEventDTO
        {
            [Parameter("uint256", "campaignId", 1, true)] public BigInteger CampaignId { get; set; }
            [Parameter("uint256", "requestId", 2, true)] public BigInteger RequestId { get; set; }
            [Parameter("address", "requester", 3, true)] public string Requester { get; set; }
            [Parameter("uint256", "amount", 4, false)] public BigInteger Amount { get; set; }
            [Parameter("string", "reason", 5, false)] public string Reason { get; set; }
            [Parameter("uint256", "voteEndTime", 6, false)] public BigInteger VoteEndTime { get; set; }
        }

        [Nethereum.ABI.FunctionEncoding.Attributes.Event("VoteCast")]
        public class VoteCastEventDTO : IEventDTO
        {
            [Parameter("uint256", "campaignId", 1, true)] public BigInteger CampaignId { get; set; }
            [Parameter("uint256", "requestId", 2, true)] public BigInteger RequestId { get; set; }
            [Parameter("address", "voter", 3, true)] public string Voter { get; set; }
            [Parameter("bool", "agree", 4, false)] public bool Agree { get; set; }
            [Parameter("uint256", "voteWeight", 5, false)] public BigInteger VoteWeight { get; set; }
        }

        //[Nethereum.ABI.FunctionEncoding.Attributes.Event("WithdrawalExecuted")]
        //public class WithdrawalExecutedEventDTO : IEventDTO
        //{
        //    [Parameter("string", "status", 1, false)] public byte Status { get; set; }
        //    [Parameter("uint256", "campaignId", 2, true)] public BigInteger CampaignId { get; set; }
        //    [Parameter("uint256", "requestId", 3, true)] public BigInteger RequestId { get; set; }
        //    [Parameter("address", "recipient", 4, true)] public string Recipient { get; set; }
        //    [Parameter("uint256", "amount", 5, false)] public BigInteger Amount { get; set; }
        //}


        [Nethereum.ABI.FunctionEncoding.Attributes.Event("RefundIssued")]
        public class RefundIssuedEventDTO : IEventDTO
        {
            [Parameter("uint256", "campaignId", 1, true)]
            public BigInteger CampaignId { get; set; }

            [Parameter("address", "investor", 2, true)]
            public string Investor { get; set; }

            [Parameter("uint256", "amount", 3, false)]
            public BigInteger Amount { get; set; }
        }


        [Nethereum.ABI.FunctionEncoding.Attributes.Event("CampaignStatusUpdated")]
        public class CampaignStatusUpdatedEventDTO : IEventDTO
        {
            [Parameter("uint256", "campaignId", 1, true)]
            public BigInteger CampaignId { get; set; }

            [Parameter("uint8", "newStatus", 2, false)] // uint8 tương ứng enum trong Solidity
            public byte NewStatus { get; set; }
        }


        [Nethereum.ABI.FunctionEncoding.Attributes.Event("ProfitAdded")]

        public class ProfitAddedEventDTO : IEventDTO
        {
            [Parameter("uint256", "id", 0, true)]       // indexed id (first indexed)
            public BigInteger Id { get; set; }

            [Parameter("uint256", "campaignId", 1, true)]  // indexed campaignId (second indexed)
            public BigInteger CampaignId { get; set; }

            [Parameter("uint256", "amount", 2, false)]     // non-indexed amount (third parameter)
            public BigInteger Amount { get; set; }
        }


        [Nethereum.ABI.FunctionEncoding.Attributes.Event("ProfitClaimed")]
        public class ProfitClaimedEventDTO : IEventDTO
        {
            [Parameter("uint256", "campaignId", 1, true)]
            public BigInteger CampaignId { get; set; }

            [Parameter("address", "investor", 2, true)]
            public string Investor { get; set; }

            [Parameter("uint256", "amount", 3, false)]
            public BigInteger Amount { get; set; }
        }


        [Nethereum.ABI.FunctionEncoding.Attributes.Event("CampaignCanceledByAdmin")]
        public class CampaignCanceledByAdminEventDTO : IEventDTO
        {
            [Parameter("uint256", "campaignId", 1, true)]
            public BigInteger CampaignId { get; set; }

            [Parameter("address", "admin", 2, true)]
            public string Admin { get; set; }
        }


        [Nethereum.ABI.FunctionEncoding.Attributes.Event("SetFees")]
        public class SetFeesEventDTO : IEventDTO
        {
            [Parameter("address", "receiver", 1, true)]
            public string Receiver { get; set; }

            [Parameter("uint16", "fees", 2, false)]
            public ushort Fees { get; set; }
        }


        [Nethereum.ABI.FunctionEncoding.Attributes.Event("SetAddressReceiver")]
        public class SetAddressReceiverEventDTO : IEventDTO
        {
            [Parameter("address", "receiver", 1, true)]
            public string Receiver { get; set; }

            [Parameter("address", "admin", 2, true)]
            public string Admin { get; set; }
        }

    }
}
