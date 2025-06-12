
namespace PokedexAnswer
{
    using Durandal.API;

    using Durandal.Common.Logger;
    using Durandal.Common.Dialog;
    using Durandal.Common.Dialog.Services;

    using Durandal.Common.Utils;
    using System.Collections.Generic;
    using Durandal.Common.IO;
    using System.Threading.Tasks;
    using Durandal.Common.NLP;
    using Durandal.Common.File;
    using Durandal;
    using Durandal.Common.Statistics;
    using Durandal.Common.NLP.Language;

    public class PokedexAnswer : DurandalPlugin
    {
        private IList<Pokemon> _pokemon;
        
        public PokedexAnswer()
            : base("pokedex")
        {
        }

        public override async Task OnLoad(IPluginServices services)
        {
            // Load the database
            _pokemon = new List<Pokemon>();
            VirtualPath database = services.PluginDataDirectory + "\\pokemon.dat";
            if (services.FileSystem.Exists(database))
            {
                foreach (string line in await services.FileSystem.ReadLinesAsync(database))
                {
                    if (string.IsNullOrEmpty(line))
                        continue;

                    string[] parts = line.Split('\t');
                    if (parts.Length == 5)
                    {
                        _pokemon.Add(new Pokemon()
                            {
                                Number = int.Parse(parts[0]),
                                Name = parts[1],
                                EvolvesFrom = parts[2],
                                InfoLink = parts[3],
                                ThumbImgLink = parts[4]
                            });
                    }
                }

                services.Logger.Log("Loaded information about " + _pokemon.Count + " pokemon");
            }
            else
            {
                services.Logger.Log("Pokemon database was not found!", LogLevel.Wrn);
            }
        }

        public override async Task<PluginResult> Execute(QueryWithContext queryWithContext, IPluginServices services)
        {
            Pokemon pokemon = await ResolvePokemonFromSlotValue(queryWithContext.Understanding, queryWithContext.ClientContext.Locale, services);

            if (pokemon == null)
            {
                services.Logger.Log("No pokemon reference found; ignoring input");
                return new PluginResult(Result.Skip);
            }

            PokemonStatisticsView view = await BulbapediaInterface.GenerateHtmlPageForPokemon(pokemon.InfoLink, services.Logger);

            return new PluginResult(Result.Success)
                {
                    ResponseHtml = view.Render()
                };
        }

        private async Task<Pokemon> ResolvePokemonFromSlotValue(RecoResult recoResult, LanguageCode locale, IPluginServices services)
        {
            LexicalString pokemonName = DialogHelpers.TryGetLexicalSlotValue(recoResult, "pkmn_name");
            string pokemonNumber = DialogHelpers.TryGetSlotValue(recoResult, "pkmn_number");

            // Try and resolve pokemon by name
            if (pokemonName != null &&
                !string.IsNullOrEmpty(pokemonName.WrittenForm))
            {
                IList<NamedEntity<Pokemon>> allPokemon = new List<NamedEntity<Pokemon>>();
                foreach (Pokemon m in _pokemon)
                {
                    allPokemon.Add(new NamedEntity<Pokemon>(m, new List<LexicalString>() { new LexicalString(m.Name) }));
                }

                IList<Hypothesis<Pokemon>> resolvedPokemon = await services.EntityResolver.ResolveEntity<Pokemon>(pokemonName, allPokemon, locale, services.Logger);
                if (resolvedPokemon.Count > 0 && resolvedPokemon[0].Conf > 0.4)
                {
                    return resolvedPokemon[0].Value;
                }
            }

            // Try and resolve pokemon by number
            if (!string.IsNullOrEmpty(pokemonNumber))
            {
                int num;
                if (int.TryParse(pokemonNumber, out num) && num > 1 && num <= _pokemon.Count)
                {
                    return _pokemon[num - 1];
                }
                services.Logger.Log("Could not resolve pokemon #" + pokemonNumber);
            }

            return null;
        }
    }
}
