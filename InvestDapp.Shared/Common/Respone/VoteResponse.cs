namespace InvestDapp.Shared.Common.Respone
{
    public class VoteResponse
    {
        public int Id { get; set; }
        public string VoterAddress { get; set; }
        public bool Agreed { get; set; }
        public double VoteWeight { get; set; }
    }

}