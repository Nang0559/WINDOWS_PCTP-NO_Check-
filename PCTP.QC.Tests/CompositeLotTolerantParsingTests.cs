using System.Linq;
using PCTP.Common;
using Xunit;

namespace PCTP.QC.Tests
{
    public sealed class CompositeLotTolerantParsingTests
    {
        private const string A = "26110400167000811700";
        private const string B = "26110500167000811700";

        [Fact]
        public void SplitsEveryLotWithItsOwnQuantity()
        {
            var parts = LotCodeHelper.ParseCompositeLotTolerant(A + "-1," + B + "-2", null);
            Assert.Equal(2, parts.Count);
            Assert.Equal(A, parts[0].LotNo); Assert.Equal(1m, parts[0].Quantity);
            Assert.Equal(B, parts[1].LotNo); Assert.Equal(2m, parts[1].Quantity);
            Assert.True(parts.All(x => x.IsValid && x.HasQuantity));
        }

        [Fact]
        public void BadTokenDoesNotHideTheOtherLots()
        {
            var parts = LotCodeHelper.ParseCompositeLotTolerant(A + "-100,XYZ-5," + B + "-200", null);
            Assert.Equal(3, parts.Count);
            Assert.True(parts[0].IsValid);
            Assert.False(parts[1].IsValid);
            Assert.True(parts[2].IsValid);
        }

        [Fact]
        public void SingleLotWithoutQuantityUsesFallback()
        {
            var parts = LotCodeHelper.ParseCompositeLotTolerant(A, 800m);
            Assert.Single(parts);
            Assert.True(parts[0].HasQuantity);
            Assert.Equal(800m, parts[0].Quantity);
        }

        [Fact]
        public void SearchKeyIsFirst20CharsOfPrintedLot()
        {
            Assert.Equal(A, LotCodeHelper.GetSearchKey(A + "00010400"));
            Assert.Equal(A.Substring(0, 13), LotCodeHelper.GetSearchKey(A.Substring(0, 15)));
        }
    }
}
