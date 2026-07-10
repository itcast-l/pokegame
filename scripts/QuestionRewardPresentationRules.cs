public static class QuestionRewardPresentationRules
{
	public static bool ShouldDelayTableRenderForQuestionReward(bool hasQuestionRewardAnimation, bool isLocalHandPlayer)
	{
		return hasQuestionRewardAnimation && isLocalHandPlayer;
	}
}
