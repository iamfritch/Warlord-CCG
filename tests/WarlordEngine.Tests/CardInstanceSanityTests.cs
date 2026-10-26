using Xunit;

namespace WarlordEngine.Tests;

public sealed class CardInstanceSanityTests
{
	[Fact]
	public void CardInstance_AttachedItemIds_And_StatusTags_Add_Persists_On_Instance()
	{
		CardInstance cardInstance = new CardInstance
		{
			InstanceId = 1,
			DefinitionId = "test-fighter"
		};
		CardInstance cardInstance2 = cardInstance with
		{
			AttachedItemIds = [ 42, 43 ]
		};
		Assert.Equal(2, cardInstance2.AttachedItemIds.Count);
		Assert.Contains(42, cardInstance2.AttachedItemIds);
		Assert.Contains(43, cardInstance2.AttachedItemIds);
		cardInstance2.StatusTags.Add("stunned");
		cardInstance2.StatusTags.Add("bonus_ac:2");
		Assert.Equal(2, cardInstance2.StatusTags.Count);
		Assert.Contains("stunned", cardInstance2.StatusTags);
		Assert.Contains("bonus_ac:2", cardInstance2.StatusTags);
		CardInstance cardInstance3 = cardInstance2.DeepClone()with
		{
			AttachedItemIds = [ 99 ],
			StatusTags = [ "blessed" ]
		};
		Assert.DoesNotContain(99, cardInstance2.AttachedItemIds);
		Assert.DoesNotContain("blessed", cardInstance2.StatusTags);
		Assert.Contains(99, cardInstance3.AttachedItemIds);
		Assert.Contains("blessed", cardInstance3.StatusTags);
	}
}
