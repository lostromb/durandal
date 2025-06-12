namespace Durandal.Common.NLP.Feature
{
	public class TrainingEvent : ITrainingFeature
	{
        /// <summary>
        /// The outcome label for this training event.
        /// </summary>
        public string Outcome;

        /// <summary>
        /// A string array of context values for this training event.
        /// </summary>
        public string[] Context;

		/// <summary>
		/// Constructor for a training event.
		/// </summary>
		/// <param name="outcome">
		/// the outcome label
		/// </param>
		/// <param name="context">
		/// array containing context values
		/// </param>
		public TrainingEvent(string outcome, string[] context)
		{
			Outcome = outcome;
            Context = context;
		}
		
		/// <summary>
		/// Override providing text summary of the training event.
		/// </summary>
		/// <returns>
		/// Summary of the training event.
		/// </returns>
		public override string ToString()
		{
			return Outcome + " " + string.Join(", ", Context);
		}

        public bool Parse(string input)
        {
            int spaceIdx = input.IndexOf(" ");
            if (spaceIdx < 0)
            {
                Outcome = input;
                Context = new string[0];
                return true;
            }

            Outcome = input.Substring(spaceIdx);
            Context = input.Substring(spaceIdx + 1).Split(',');
            return true;
        }
    }
}
