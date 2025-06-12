using Durandal.API;
using Durandal.Common.Logger;
using Durandal.Common.Net.Http;
using Durandal.Common.Remoting;
using Durandal.Common.Test;
using Durandal.Common.Tasks;
using Durandal.Plugins.Recipe;
using Durandal.Plugins.Recipe.BigOven;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Threading.Tasks;
using Durandal.Common.Test.Builders;
using System;
using System.IO;

#pragma warning disable CS0219
namespace DialogTests.Plugins.Recipe
{
    [TestClass]
    public class RecipeTests
    {
        private static RecipePlugin _plugin;
        private static InqueTestDriver _testDriver;

        #region Test framework

        [ClassInitialize]
        public static void Initialize(TestContext context)
        {
            _plugin = new RecipePlugin(new PortableHttpClientFactory());
            string rootEnv = context.Properties["DurandalRootDirectory"]?.ToString();
            if (string.IsNullOrEmpty(rootEnv))
            {
                rootEnv = Environment.GetEnvironmentVariable("DURANDAL_ROOT");
                if (string.IsNullOrEmpty(rootEnv))
                {
                    throw new FileNotFoundException("Cannot find durandal environment directory, either from DurandalRootDirectory test property, or DURANDAL_ROOT environment variable.");
                }
            }

            InqueTestParameters testConfig = PluginTestCommon.CreateTestParameters(_plugin, "RecipePlugin.dupkg", new DirectoryInfo(rootEnv));
            //testConfig.PluginProviderFactory = AppDomainIsolatedPluginProvider.BuildContainerizedPluginProvider;
            _testDriver = new InqueTestDriver(testConfig);
            _testDriver.Initialize().Await();
        }

        [ClassCleanup]
        public static void Cleanup()
        {
            _testDriver.Dispose();
        }

        [TestInitialize]
        public void TestInitialize()
        {
            _testDriver.ResetState();
        }

        #endregion

        #region Tests

        [TestMethod]
        public async Task TestBigovenApi()
        {
            ILogger logger = new ConsoleLogger();
            IHttpClientFactory httpFac = new PortableHttpClientFactory();
            BigOvenService service = new BigOvenService("07pJZqH7YZrY85Q42L11jp1U74Om368N", httpFac, logger);
            List<string> allQueries = new List<string>();
            allQueries.Add("banana bread");
            allQueries.Add("chicken adobo");
            allQueries.Add("pumpkin pie");
            allQueries.Add("garlic alfredo");
            foreach (string query in allQueries)
            {
                var recipes = await service.SearchRecipes(query, logger);
                foreach (var recipe in recipes)
                {
                    var recipeDetails = await service.GetRecipeDetail(int.Parse(recipe.SourceId), logger);
                }
            }
        }

        [TestMethod]
        public void TestBigovenParseInstructionsList1()
        {
            string rawInstructions =
                "Mash bananas with fork until smooth.  Combine with all other ingredients and mix until smooth.  Pour into a large greased loaf pan.  Bake at 350 degrees F. for about one hour";

            string[] expectedInstructions = new string[]
            {
                "Mash bananas with fork until smooth.",
                "Combine with all other ingredients and mix until smooth.",
                "Pour into a large greased loaf pan.",
                "Bake at 350 degrees F. for about one hour." // Convert "350 F" to a "350 degrees fahrenheit" readout?
            };
        }

        [TestMethod]
        public void TestBigovenParseInstructionsList2()
        {
            string rawInstructions =
                "After cubing the chicken, place butter/margarine in skillet or saute pan.  Put burner on medium heat.  Place garlic into the butter, then place chicken in skillet.  Brown chicken in the butter and garlic mixture until fully cooked.  Pour Alfredo sauce into saucepan, and cook thoroughly over medium heat.  After all ingredients are cooked, you may start assembling.  \r\n" +
                "\r\n" +
                "Start by placing fettuccine noodles on a plate.Pour Alfredo sauce over top, then top with the garlic chicken mixture.\r\n" +
                "\r\n" +
                "Lastly, ENJOY!";

            string[] expectedInstructions = new string[]
            {
                "After cubing the chicken, place butter/margarine in skillet or saute pan.",
                "Put burner on medium heat.",
                "Place garlic into the butter, then place chicken in skillet.",
                "Brown chicken in the butter and garlic mixture until fully cooked.",
                "Pour Alfredo sauce into saucepan, and cook thoroughly over medium heat.",
                "After all ingredients are cooked, you may start assembling.",
                "Start by placing fettuccine noodles on a plate.",
                "Pour Alfredo sauce over top, then top with the garlic chicken mixture.",
                "Lastly, ENJOY!"
            };
        }

        [TestMethod]
        public void TestBigovenParseInstructionsList3()
        {
            string rawInstructions =
                "Sprinkle lemon pepper, black pepper, onion powder, garlic salt, cayenne pepper, and paprika on one side of the chicken breast, in order listed, using more or less to taste.  Turn chicken over and repeat on other side.  Coat the chicken liberally with the italian dressing.  Refrigerate for 1-2 hours while preparing sauce.  \r\n" +
                "\r\n" +
                "Heat olive oil in a heavy sauce pan.  Add the mushrooms, finely diced onions, and minced garlic.  Sprinkle to taste, coarse ground black pepper, garlic salt, cayenne pepper, paprika, parsley, lemon pepper ad cumin.  Saute until onions are clear and mushrooms are nicely browned.  Empty two jars of alfredo sauce into a medium sized sauce pan and add the whipping cream.  Add the cooked mushroom mixture and stir to blend.  Heat the sauce over low heat stirring occasionally.  Do not allow to boil.  Add 1 Tbls parsley flakes, 1 tsp italian seasoning, 1/2 tsp oregano, 1/2 tsp celery salt, 1/2 tsp cumin, 1/4 tsp cayenne pepper, and 1 tsp coarse ground black pepper.  Add one cup of white wine if desired.  Allow to simmer over low heat while cooking the chicken and pasta.\r\n" +
                "\r\n" +
                "Cook the pasta according to directions, adding olive oil and salt to taste.  \r\n" +
                "\r\n" +
                "Grill chicken until done, slice and place on top of the pasta and sauce.  Sprinkle with parsley and shredded parmesan cheese to taste.  \r\n" +
                "\r\n" +
                "Serve with garlic bread if desired.";

            string[] expectedInstructions = new string[]
            {
                "Sprinkle lemon pepper, black pepper, onion powder, garlic salt, cayenne pepper, and paprika on one side of the chicken breast, in order listed, using more or less to taste.",
                "Turn chicken over and repeat on other side.",
                "Coat the chicken liberally with the italian dressing.",
                "Refrigerate for 1-2 hours while preparing sauce.",
                "Heat olive oil in a heavy sauce pan.",
                "Add the mushrooms, finely diced onions, and minced garlic.",
                "Sprinkle to taste, coarse ground black pepper, garlic salt, cayenne pepper, paprika, parsley, lemon pepper ad cumin.",
                "Saute until onions are clear and mushrooms are nicely browned.",
                "Empty two jars of alfredo sauce into a medium sized sauce pan and add the whipping cream.",
                "Add the cooked mushroom mixture and stir to blend.",
                "Heat the sauce over low heat stirring occasionally.",
                "Do not allow to boil.",
                "Add 1 Tbls parsley flakes, 1 tsp italian seasoning, 1/2 tsp oregano, 1/2 tsp celery salt, 1/2 tsp cumin, 1/4 tsp cayenne pepper, and 1 tsp coarse ground black pepper.",
                "Add one cup of white wine if desired.",
                "Allow to simmer over low heat while cooking the chicken and pasta.",
                "Cook the pasta according to directions, adding olive oil and salt to taste.",
                "Grill chicken until done, slice and place on top of the pasta and sauce.",
                "Sprinkle with parsley and shredded parmesan cheese to taste.",
                "Serve with garlic bread if desired."
            };
        }

        [TestMethod]
        public void TestBigovenParseInstructionsList4()
        {
            string rawInstructions =
                "1. Turn stove on medium heat and put olive oil in skillet - 2 Tablespooon\r\n" + // Remove numbered list
                "\r\n" +
                "2. Add onion, garlic, and  3-4 bay leaves\r\n" +
                "\r\n" +
                "3. Add chicken, soy sauce, and vinegar.\r\n" +
                "\r\n" +
                "4. Turn it to high - bring it to a boil - then reduce heat to medium and cover.\r\n" +
                "Add timer for 15 minutes - when it rings, flip chicken over - cover\r\n" +
                "\r\n" +
                "5. Put timer on for another 15 minutes and when it rings,turn off burner and serve immediately.\r\n" +
                "\r\n" +
                "\r\n" +
                "Note: If your going to review my recipe, don't give my recipe a poor score if you don't even follow the instructions and add/subtract your own preference. Make the recipe accordingly, and rate it based on those specifications.";

            string[] expectedInstructions = new string[]
            {
                "Turn stove on medium heat and put olive oil in skillet - 2 Tablespooon.",
                "Add onion, garlic, and  3-4 bay leaves.",
                "Add chicken, soy sauce, and vinegar.",
                "Turn it to high - bring it to a boil - then reduce heat to medium and cover.",
                "Add timer for 15 minutes - when it rings, flip chicken over - cover.",
                "Put timer on for another 15 minutes and when it rings,turn off burner and serve immediately." // fix comma spacing?
            };
        }

        [TestMethod]
        public void TestBigovenParseInstructionsList5()
        {
            string rawInstructions =
                "Place all ingredients in a pot, and make sure chicken is half-submerged or more. Bring the mixture to a boil, stirring regularly. Once boiling, reduce heat to a simmer and leave it for 30-45 minutes, covered. For every ten minutes, stir the mixture and turn the chicken. Before serving, remove the bay leaves.\r\n" +
                "\r\n" +
                "Optional: Separate chicken from mixture and saute it if you want it to be crispy. Place back in stew before serving.";

            string[] expectedInstructions = new string[]
            {
                "Place all ingredients in a pot, and make sure chicken is half-submerged or more.",
                "Bring the mixture to a boil, stirring regularly.",
                "Once boiling, reduce heat to a simmer and leave it for 30-45 minutes, covered. For every ten minutes, stir the mixture and turn the chicken.",
                "Before serving, remove the bay leaves.",
                "Optional: Separate chicken from mixture and saute it if you want it to be crispy.",
                "Place back in stew before serving." // does the note go on a separate line?
            };
        }

        [TestMethod]
        public void TestBigovenParseInstructionsList6()
        {
            string rawInstructions =
                "Mix marinade ingredients in a small saucepan and bring to a boil. Allow to steep for a few minutes while the wings are washed and prepped. \r\n" +
                "\r\n" +
                "Wash the wings and separate them into first and second joints. Place them in a large bowl or plastic bag. Pour the warm marinade over the wings and mix to coat all portions. Store in refrigerator at least eight hours. Overnight marination is even better. Mix periodically thoughout the marination process. \r\n" +
                "\r\n" +
                "Preheat oven to 375F. \r\n" +
                "\r\n" +
                "Spread the wings in a large shallow pan and pour some of the marinade over them. Bake at 375F until well browned and tender. About 45 minutes in a conventional oven or 30 under convection. \r\n" +
                "\r\n" +
                "To crisp the wings remove them from the pan and drain on paper towel. Place them under the broiler for 2-3 minutes watching carefully for burning. \r\n" +
                "\r\n" +
                "We served these with well chilled dark beer. \r\n" +
                "\r\n" +
                "Each (4-6 wing) serving contains an estimated:\r\n" + // remove nutrition info
                "Cals: 326, FatCals: 172, TotFat: 20g\r\n" +
                "SatFat: 5g, PolyFat: 4g, MonoFat: 11g\r\n" +
                "Chol: 84mg, Na: 600mg, K: 319mg\r\n" +
                "TotCarbs: 8g, Fiber: 1g, Sugars: 2g\r\n" +
                "NetCarbs: 7g, Protein: 29g\r\n" +
                "\r\n" +
                "\r\n" +
                "Adapted from Chile Pepper Magazine, Apr. 2007"; // remove attribution

            string[] expectedInstructions = new string[]
            {
                "Mix marinade ingredients in a small saucepan and bring to a boil.",
                "Allow to steep for a few minutes while the wings are washed and prepped.",
                "Wash the wings and separate them into first and second joints.",
                "Place them in a large bowl or plastic bag.",
                "Pour the warm marinade over the wings and mix to coat all portions.",
                "Store in refrigerator at least eight hours.",
                "Overnight marination is even better.",
                "Mix periodically thoughout the marination process.",
                "Preheat oven to 375F.",
                "Spread the wings in a large shallow pan and pour some of the marinade over them.",
                "Bake at 375F until well browned and tender.",
                "About 45 minutes in a conventional oven or 30 under convection.",
                "To crisp the wings remove them from the pan and drain on paper towel.",
                "Place them under the broiler for 2-3 minutes watching carefully for burning.",
                "We served these with well chilled dark beer." // this can potentially go to notes?
            };
        }

        [TestMethod]
        public void TestBigovenParseInstructionsList7()
        {
            string rawInstructions =
                "In a large kettle combine the chicken, the vinegar, the garlic, the bay  leaves, the peppercorns, and 1 cup water, bring the mixture to a boil, and  simmer it, covered, for 20 minutes. Add the soy sauce and simmer the  mixture, covered, for 20 minutes. Transfer the chicken with tongs to a  plate and boil the liquid for 10 minutes, or until it is reduced to about 1  cup. Let the sauce cool, remove the bay leaves, and skim the fat from the  surface.    \r\n" +
                "\r\n" +
                "In a large skillet heat the oil over high heat until it is hot but not  smoking and in it saute the chicken, patted dry, in batches, turning it,  for 5 minutes, or until it is browned well. Transfer the chicken to a  rimmed platter, pour the sauce, heated, over it, and serve the chicken with  the rice.    \r\n" +
                "\r\n" +
                "Serves 4 to 8.    \r\n" +
                "\r\n" +
                "Gourmet June 1991"; // Remove attribution tag

            string[] expectedInstructions = new string[]
            {
                "In a large kettle combine the chicken, the vinegar, the garlic, the bay  leaves, the peppercorns, and 1 cup water, bring the mixture to a boil, and  simmer it, covered, for 20 minutes.", // Fix strange spacing
                "Add the soy sauce and simmer the  mixture, covered, for 20 minutes.",
                "Transfer the chicken with tongs to a  plate and boil the liquid for 10 minutes, or until it is reduced to about 1  cup.",
                "Let the sauce cool, remove the bay leaves, and skim the fat from the  surface.",
                "In a large skillet heat the oil over high heat until it is hot but not  smoking and in it saute the chicken, patted dry, in batches, turning it,  for 5 minutes, or until it is browned well.",
                "Transfer the chicken to a  rimmed platter, pour the sauce, heated, over it, and serve the chicken with  the rice."
            };
        }

        [TestMethod]
        public void TestBigovenParseInstructionsList8()
        {
            string rawInstructions = 
                "Combine all ingredients in a sauce pan and marinate for two hours. Boil  mixture till chicken is tender.  Separate sauce from chicken and broil  chicken until brown.  Reduce the sauce over moderate heat to half and pour  over chicken.    Serve Hot with rice.    Mark Soennichsen    File ftp://ftp.idiscover.co.uk/pub/food/mealmaster/recipes/mmdjaxxx.zip";

            string[] expectedInstructions = new string[]
            {
                "Combine all ingredients in a sauce pan and marinate for two hours.",
                "Boil  mixture till chicken is tender.",
                "Separate sauce from chicken and broil  chicken until brown.",
                "Reduce the sauce over moderate heat to half and pour  over chicken.",
                "Serve Hot with rice."
            };
        }

        [TestMethod]
        public void TestBigovenParseInstructionsList9()
        {
            string rawInstructions = 
                "Mix sugar, salt, cinnamon, ginger, cloves in a small bowl.  Beat eggs in large bowl.  Stir in pumpkin and sugar/spice mixture.  Gradually stir in evaporated milk. Bake in preheated 425 degree oven for 15 minutes.  Reduce temperature to 350 degrees. Bake 40 to 50 minutes or until knife inserted in the center comes out clean.  Cool on rack for 2 hours. Nutrition (calculated from recipe ingredients) ----------------------------------------------  Calories: 177  Calories From Fat: 47  Total Fat: 5.3g  Cholesterol: 65.2mg  Sodium: 349.2mg  Potassium: 260.4mg  Carbohydrates: 28.7g  Fiber: 1.8g  Sugar: 25g  Protein: 5.2g";

            string[] expectedInstructions = new string[]
            {
                "Mix sugar, salt, cinnamon, ginger, cloves in a small bowl.",
                "Beat eggs in large bowl.",
                "Stir in pumpkin and sugar/spice mixture.",
                "Gradually stir in evaporated milk.",
                "Bake in preheated 425 degree oven for 15 minutes.",
                "Reduce temperature to 350 degrees.",
                "Bake 40 to 50 minutes or until knife inserted in the center comes out clean.",
                "Cool on rack for 2 hours."
            };
        }

        [TestMethod]
        public void TestBigovenParseInstructionsList10()
        {
            string rawInstructions = 
                "Preheat the oven to 350 degrees F.\r\n" +
                "\r\n" +
                "Place 1 piece of pre-made pie dough down into a (9-inch) pie pan and press down along the bottom and all sides. Pinch and crimp the edges together to make a pretty pattern. Put the pie shell back into the freezer for 1 hour to firm up. Fit a piece of aluminum foil to cover the inside of the shell completely. Fill the shell up to the edges with pie weights or dried beans (about 2 pounds) and place it in the oven. Bake for 10 minutes, remove the foil and pie weights and bake for another 10 minutes or until the crust is dried out and beginning to color.\r\n" +
                "\r\n" +
                "For the filling, in a large mixing bowl, beat the cream cheese with a hand mixer. Add the pumpkin and beat until combined. Add the sugar and salt, and beat until combined. Add the eggs mixed with the yolks, half-and-half, and melted butter, and beat until combined. Finally, add the vanilla, cinnamon, and ginger, if using, and beat until incorporated.\r\n" +
                "\r\n" +
                "Pour the filling into the warm prepared pie crust and bake for 50 minutes, or until the center is set. Place the pie on a wire rack and cool to room temperature. Cut into slices and top each piece with a generous amount of whipped cream.";

            string[] expectedInstructions = new string[]
            {
                "Preheat the oven to 350 degrees F.",
                "Place 1 piece of pre-made pie dough down into a (9-inch) pie pan and press down along the bottom and all sides.",
                "Pinch and crimp the edges together to make a pretty pattern.",
                "Put the pie shell back into the freezer for 1 hour to firm up.",
                "Fit a piece of aluminum foil to cover the inside of the shell completely.",
                "Fill the shell up to the edges with pie weights or dried beans (about 2 pounds) and place it in the oven.",
                "Bake for 10 minutes, remove the foil and pie weights and bake for another 10 minutes or until the crust is dried out and beginning to color.",
                "For the filling, in a large mixing bowl, beat the cream cheese with a hand mixer.",
                "Add the pumpkin and beat until combined.",
                "Add the sugar and salt, and beat until combined.",
                "Add the eggs mixed with the yolks, half-and-half, and melted butter, and beat until combined.",
                "Finally, add the vanilla, cinnamon, and ginger, if using, and beat until incorporated.",
                "Pour the filling into the warm prepared pie crust and bake for 50 minutes, or until the center is set.",
                "Place the pie on a wire rack and cool to room temperature.",
                "Cut into slices and top each piece with a generous amount of whipped cream."
            };
        }

        [TestMethod]
        public void TestBigovenParseInstructionsList11()
        {
            string rawInstructions =
                "MIX sugar, cinnamon, salt, ginger and cloves in small bowl. Beat eggs in large bowl. Stir in pumpkin and sugar-spice mixture. Gradually stir in evaporated milk.\r\n" + // Normalize case for words
                "\r\n" +
                "POUR into pie shell.\r\n" +
                "\r\n" +
                "BAKE in preheated 425° F oven for 15 minutes. Reduce temperature to 350° F; bake for 40 to 50 minutes or until knife inserted near center comes out clean. Cool on wire rack for 2 hours. Serve immediately or refrigerate. Top with whipped cream before serving.";

            string[] expectedInstructions = new string[]
            {
                "MIX sugar, cinnamon, salt, ginger and cloves in small bowl.", // Can normalize the case for some of these words
                "Beat eggs in large bowl.",
                "Stir in pumpkin and sugar-spice mixture.",
                "Gradually stir in evaporated milk.",
                "POUR into pie shell.",
                "BAKE in preheated 425° F oven for 15 minutes.",
                "Reduce temperature to 350° F; bake for 40 to 50 minutes or until knife inserted near center comes out clean.",
                "Cool on wire rack for 2 hours.",
                "Serve immediately or refrigerate.",
                "Top with whipped cream before serving."
            };
        }

        [TestMethod]
        public void TestBigovenParseInstructionsList12()
        {
            string rawInstructions =
                "Preheat the oven to 375 degrees.\r\n" +
                "\r\n" +
                "In a large bowl, whisk together the pumpkin puree, milk, half-and-half, eggs, vanilla, cinnamon, nutmeg, allspice, stevia, and salt. \r\n" +
                "Pour into the baked pie crust.\r\n" +
                "Cover the edges of the crust with aluminum foil to prevent them from over-browning.\r\n" +
                "Place the pie on a baking sheet and bake for 50 minutes, or until the filling has slightly puffed and set in the center. \r\n" +
                "If you want the crust a bit browner, remove the foil during the last 5 to 10 minutes of baking. \r\n" +
                "\r\n" +
                "Let cool before slicing. Store covered in the refrigerator.\r\n" +
                "\r\n" +
                "Pumpkin pie can be made a day ahead. Or, bake the crust and prepare the filling the night before. Then assemble the pie and bake it the day you plan to serve it.";

            string[] expectedInstructions = new string[]
            {
                "Preheat the oven to 375 degrees.",
                "In a large bowl, whisk together the pumpkin puree, milk, half-and-half, eggs, vanilla, cinnamon, nutmeg, allspice, stevia, and salt.",
                "Pour into the baked pie crust.",
                "Cover the edges of the crust with aluminum foil to prevent them from over-browning.",
                "Place the pie on a baking sheet and bake for 50 minutes, or until the filling has slightly puffed and set in the center.",
                "If you want the crust a bit browner, remove the foil during the last 5 to 10 minutes of baking.",
                "Let cool before slicing. Store covered in the refrigerator.",
                "Pumpkin pie can be made a day ahead.", // This can go to notes
                "Or, bake the crust and prepare the filling the night before.",
                "Then assemble the pie and bake it the day you plan to serve it."
            };
        }

        [TestMethod]
        public void TestBigovenParseInstructionsList13()
        {
            string rawInstructions = 
                "Preheat oven to 425 degrees F.\r\n" +
                "\r\n" +
                "Combine pumpkin, cinnamon, ginger, nutmeg, cloves, and allspice in a saucepan. Heat gently to meld flavors. Set aside and allow to cool. Add sugar and brown sugar.\r\n" +
                "\r\n" +
                "Beat egg yolks lightly in large bowl. Stir in pumpkin mixture. Gradually stir in evaporated milk.\r\n" +
                "\r\n" +
                "In a large bowl, whip egg whites until soft peaks form. Gently fold egg whites into pumpkin mixture. Pour filling into pie shell.\r\n" +
                "\r\n" +
                "Bake for 15 minutes, then reduce oven temperature to 350 degrees F. Continue baking for 40 to 50 minutes or until knife inserted near center comes out clean. Cool on wire rack for 2 hours. Serve immediately or refrigerate. Garnish as desired. (Note: Do not freeze as this will cause the crust to separate from the filling.)";

            string[] expectedInstructions = new string[]
            {
                "Preheat oven to 425 degrees F.",
                "Combine pumpkin, cinnamon, ginger, nutmeg, cloves, and allspice in a saucepan.",
                "Heat gently to meld flavors.",
                "Set aside and allow to cool.",
                "Add sugar and brown sugar.",
                "Beat egg yolks lightly in large bowl.",
                "Stir in pumpkin mixture.",
                "Gradually stir in evaporated milk.",
                "In a large bowl, whip egg whites until soft peaks form.",
                "Gently fold egg whites into pumpkin mixture.",
                "Pour filling into pie shell.",
                "Bake for 15 minutes, then reduce oven temperature to 350 degrees F.",
                "Continue baking for 40 to 50 minutes or until knife inserted near center comes out clean.",
                "Cool on wire rack for 2 hours.",
                "Serve immediately or refrigerate.",
                "Garnish as desired." // "Do not freeze" should go to notes
            };
        }
        
        [TestMethod]
        public async Task TestBigovenRecipeSearchRecipes()
        {
            DialogRequest request =
                new DialogRequestBuilder<DialogRequest>((x) => x, "banana bread recipes", InputMethod.Typed)
                    .AddRecoResult("recipe", "find_recipe", 0.95f)
                        .AddTagHypothesis(0.95f)
                            .AddBasicSlot("recipe_name", "banana bread")
                        .Build()
                    .Build()
                .Build();

            DialogResponse response = (await _testDriver.Client.MakeQueryRequest(request)).Response;
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.SelectedRecoResult);
            Assert.AreEqual("find_recipe", response.SelectedRecoResult.Intent);
            Assert.IsFalse(string.IsNullOrEmpty(response.ResponseText));
            Assert.IsFalse(response.ContinueImmediately);
        }

        [TestMethod]
        public async Task TestBigovenDoNothing()
        {
            ILogger logger = new ConsoleLogger();
            Assert.IsNotNull(logger);
            await DurandalTaskExtensions.NoOpTask;
        }

        #endregion
    }
}

#pragma warning restore CS0219