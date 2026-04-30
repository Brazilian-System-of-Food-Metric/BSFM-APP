using YoloDotNet;
using YoloDotNet.Enums;
using YoloDotNet.Models;
using SkiaSharp;
using System.Linq;
using System.Collections.Generic;

namespace BSFM.Services
{
    public class YoloInferenceService
    {
        private readonly Yolo _yolo;

        // Dicionário Estático: EN -> PT
        // Mudei para public static para que você possa usar Tradutor em outros arquivos se precisar
        public static readonly Dictionary<string, string> Tradutor = new Dictionary<string, string>
        {
            // A
            { "almond", "amêndoa" },
            { "almonds", "amêndoas" },
            { "anchovies", "anchovas" },
            { "apple", "maçã" },
            { "apple-pie", "torta de maçã" },
            { "applesauce-unsweetened-canned", "purê de maçã sem açúcar enlatado" },
            { "apricot", "damasco" },
            { "apricot-dried", "damasco seco" },
            { "apricots", "damascos" },
            { "asparagus", "aspargo" },
            { "avocado", "abacate" },

            // B
            { "bacon-cooking", "bacon para cozinhar" },
            { "bacon-frying", "bacon para fritar" },
            { "baked-potato", "batata assada" },
            { "balsamic-salad-dressing", "molho balsâmico para salada" },
            { "bamboo shoots", "broto de bambu" },
            { "banana", "banana" },
            { "banana-cake", "bolo de banana" },
            { "basil", "manjericão" },
            { "bean sprouts", "broto de feijão" },
            { "beans-kidney", "feijão vermelho" },
            { "beans-white", "feijão branco" },
            { "beef", "carne bovina" },
            { "beef-cut-into-stripes-only-meat", "carne bovina cortada em tiras (somente carne)" },
            { "beef-roast", "assar carne bovina" },
            { "beef-sirloin-steak", "bife de alcatra" },
            { "beer", "cerveja" },
            { "beetroot-raw", "beterraba crua" },
            { "beetroot-steamed-without-addition-of-salt", "beterraba cozida no vapor sem adição de sal" },
            { "bell-pepper-red-raw", "pimentão vermelho cru" },
            { "bell-pepper-red-stewed-without-addition-of-fat-without-addition-of-salt", "pimentão vermelho cozido sem adição de gordura e sal" },
            { "berries", "frutas vermelhas" },
            { "birchermuesli-prepared-no-sugar-added", "bircher muesli preparado sem açúcar adicionado" },
            { "biscuit", "biscoito" },
            { "biscuit-with-butter", "biscoito com manteiga" },
            { "biscuits", "biscoitos" },
            { "black-forest-tart", "torta floresta negra" },
            { "black-olives", "azeitonas pretas" },
            { "blue-mould-cheese", "queijo azul" },
            { "blueberries", "mirtilos" },
            { "blueberry", "mirtilo" },
            { "bolognaise-sauce", "molho à bolonhesa" },
            { "bouillon", "caldo" },
            { "bouillon-vegetable", "caldo de legumes" },
            { "bouquet-garni", "buquê de ervas" },
            { "braided-white-loaf", "pão branco trançado" },
            { "brazil-nut", "castanha-do-pará" },
            { "bread", "pão" },
            { "bread-black", "pão preto" },
            { "bread-french-white-flour", "pão francês de farinha branca" },
            { "bread-grain", "pão de grãos" },
            { "bread-half-white", "pão semi-branco" },
            { "bread-meat-substitute-lettuce-sauce", "pão com substituto de carne, alface e molho" },
            { "bread-pita", "pão pita" },
            { "bread-rye", "pão de centeio" },
            { "bread-sourdough", "pão de fermentação natural" },
            { "bread-spelt", "pão de espelta" },
            { "bread-ticino", "pão ticino" },
            { "bread-toast", "torrada" },
            { "bread-white", "pão branco" },
            { "bread-whole-wheat", "pão integral de trigo" },
            { "bread-wholemeal", "pão integral" },
            { "bread-wholemeal-toast", "torrada integral" },
            { "brie", "brie" },
            { "brioche", "brioche" },
            { "broccoli", "brócolis" },
            { "buckwheat-pancake", "panqueca de trigo sarraceno" },
            { "bulgur", "trigo para quibe" },
            { "butter", "manteiga" },
            { "butter-herb", "manteiga de ervas" },
            { "butter-spread-puree-almond", "pasta de amêndoa" },

            // C
            { "cabbage", "repolho" },
            { "cake", "bolo" },
            { "cake-chocolate", "bolo de chocolate" },
            { "cake-marble", "bolo mármore" },
            { "cake-oblong", "bolo oblongo" },
            { "cake-salted", "bolo salgado" },
            { "candy", "doce" },
            { "cantonese-fried-rice", "arroz frito à cantonesa" },
            { "cappuccino", "cappuccino" },
            { "caprese-salad-tomato-mozzarella", "salada caprese (tomate e mussarela)" },
            { "carrot", "cenoura" },
            { "carrot-cake", "bolo de cenoura" },
            { "carrot-raw", "cenoura crua" },
            { "carrot-steamed-without-addition-of-salt", "cenoura cozida no vapor sem sal" },
            { "cashew", "castanha de caju" },
            { "cashew-nut", "castanha de caju" },
            { "cauliflower", "couve-flor" },
            { "celery", "aipo" },
            { "celery stick", "talos de aipo" },
            { "cervelat", "salsicha cervelat" },
            { "champagne", "champanhe" },
            { "cheddar", "cheddar" },
            { "cheese", "queijo" },
            { "cheese butter", "manteiga de queijo" },
            { "cheese-for-raclette", "queijo para raclette" },
            { "cheesecake", "cheesecake" },
            { "cherries", "cerejas" },
            { "cherry", "cereja" },
            { "chestnuts", "castanhas" },
            { "chicken", "frango" },
            { "chicken duck", "frango e pato" },
            { "chicken-breast", "peito de frango" },
            { "chicken-cut-into-stripes-only-meat", "frango cortado em tiras (somente carne)" },
            { "chicken-nuggets", "nuggets de frango" },
            { "chickpeas", "grão-de-bico" },
            { "chili-con-carne-prepared", "chili com carne preparado" },
            { "chinese-cabbage", "repolho chinês" },
            { "chips-french-fries", "batatas fritas" },
            { "chives", "cebolinha" },
            { "chocolate", "chocolate" },
            { "chocolate-egg-small", "ovo de chocolate pequeno" },
            { "chorizo", "chorizo" },
            { "cilantro mint", "coentro e hortelã" },
            { "coca-cola-zero", "coca-cola zero" },
            { "cocktail", "coquetel" },
            { "coconut", "coco" },
            { "coconut-milk", "leite de coco" },
            { "coffee", "café" },
            { "coffee-decaffeinated", "café descafeinado" },
            { "coffee-with-caffeine", "café com cafeína" },
            { "cookies", "biscoitos" },
            { "cordon-bleu-from-pork-schnitzel-fried", "cordon bleu de porco frito" },
            { "coriander", "coentro" },
            { "corn", "milho" },
            { "cottage-cheese", "queijo cottage" },
            { "country-fries", "batatas rústicas" },
            { "couscous", "couscous" },
            { "Coxa_de_frango_empanada", "coxa de frango empanada" },
            { "crab", "caranguejo" },
            { "cream", "creme" },
            { "cream-cheese", "cream cheese" },
            { "crepe-plain", "crepe simples" },
            { "crisps", "batatas chips" },
            { "croissant", "croissant" },
            { "croissant-wholegrain", "croissant integral" },
            { "croissant-with-chocolate-filling", "croissant com recheio de chocolate" },
            { "croque-monsieur", "croque-monsieur" },
            { "croutons", "croutons" },
            { "crunch-muesli", "muesli crocante" },
            { "cucumber", "pepino" },
            { "cucumber-pickled", "pepino em conserva" },

            // D
            { "dairy-ice-cream", "sorvete de leite" },
            { "dark-chocolate", "chocolate amargo" },
            { "date", "tâmara" },
            { "dates", "tâmaras" },
            { "dried cranberries", "cranberries secas" },
            { "dried-meat", "carne seca" },

            // E
            { "egg", "ovo" },
            { "egg tart", "tarte de ovo" },
            { "egg-scrambled-prepared", "ovos mexidos preparados" },
            { "eggplant", "berinjela" },
            { "eggplant-caviar", "caviar de berinjela" },
            { "emmental-cheese", "queijo emmental" },
            { "enoki mushroom", "cogumelo enoki" },
            { "espresso-with-caffeine", "espresso com cafeína" },

            // F
            { "fajita-bread-only", "pão para fajita" },
            { "falafel-balls", "bolinhos de falafel" },
            { "faux-mage-cashew-vegan-chers", "queijo vegano de castanha de caju" },
            { "fennel", "funcho" },
            { "feta", "feta" },
            { "fig", "figo" },
            { "fig-dried", "figo seco" },
            { "figs", "figos" },
            { "fish", "peixe" },
            { "fish-fingers-breaded", "dedos de peixe empanados" },
            { "flakes-oat", "flocos de aveia" },
            { "focaccia", "focaccia" },
            { "fondue", "fondue" },
            { "frango_empanado", "frango empanado" },
            { "Frango_empanado", "frango empanado" },
            { "French beans", "feijão verde" },
            { "french fries", "batatas fritas" },
            { "french-beans", "feijão verde" },
            { "french-pizza-from-alsace-baked", "pizza francesa da Alsácia assada" },
            { "french-salad-dressing", "molho francês para salada" },
            { "fresh-cheese", "queijo fresco" },
            { "fried meat", "carne frita" },
            { "fruit-coulis", "calda de frutas" },
            { "fruit-salad", "salada de frutas" },
            { "fruit-tart", "tarte de frutas" },
            { "frying-sausage", "salsicha para fritar" },

            // G
            { "garlic", "alho" },
            { "ginger", "gengibre" },
            { "gluten-free-bread", "pão sem glúten" },
            { "goat-cheese-soft", "queijo de cabra macio" },
            { "grape", "uva" },
            { "grapefruit-pomelo", "toranja" },
            { "grapes", "uvas" },
            { "greek-salad", "salada grega" },
            { "greek-yaourt-yahourt-yogourt-ou-yoghourt", "iogurte grego" },
            { "green beans", "feijão verde" },
            { "green-asparagus", "aspargo verde" },
            { "green-bean-steamed-without-addition-of-salt", "feijão verde cozido no vapor sem sal" },
            { "green-olives", "azeitonas verdes" },
            { "grits-polenta-maize-flour", "grits/farinha de milho para polenta" },
            { "gruyere", "gruyère" },
            { "guacamole", "guacamole" },

            // H
            { "halloumi", "halloumi" },
            { "ham", "presunto" },
            { "ham-cooked", "presunto cozido" },
            { "ham-raw", "presunto cru" },
            { "ham-turkey", "presunto de peru" },
            { "hamburg", "hambúrguer" },
            { "hamburger", "hambúrguer" },
            { "hamburger-bread-meat-ketchup", "hambúrguer com pão, carne e ketchup" },
            { "hamburger-bun", "pão de hambúrguer" },
            { "hanamaki baozi", "baozi hanamaki" },
            { "hard-cheese", "queijo duro" },
            { "hazelnut", "avelã" },
            { "hazelnut-chocolate-spread-nutella-ovomaltine-caotina", "pasta de chocolate com avelã (Nutella/Ovomaltine/Caotina)" },
            { "herbal-tea", "chá de ervas" },
            { "high-protein-pasta-made-of-lentils-peas", "massa rica em proteína feita de lentilhas e ervilhas" },
            { "honey", "mel" },
            { "hot_dog", "cachorro-quente" },
            { "hummus", "hummus" },

            // I
            { "ice cream", "sorvete" },
            { "italian-salad-dressing", "molho italiano para salada" },

            // J
            { "jam", "geleia" },
            { "juice", "suco" },
            { "juice-apple", "suco de maçã" },
            { "juice-multifruit", "suco multifrutas" },

            // K
            { "kaki", "caqui" },
            { "kebab-in-pita-bread", "kebab no pão pita" },
            { "kelp", "kelp" },
            { "ketchup", "ketchup" },
            { "king oyster mushroom", "cogumelo oyster rei" },
            { "kiwi", "kiwi" },
            { "kolhrabi", "couve-rábano" },

            // L
            { "lamb", "cordeiro" },
            { "lamb-chop", "costeleta de cordeiro" },
            { "lasagne-meat-prepared", "lasanha de carne preparada" },
            { "lasagne-vegetable-prepared", "lasanha de vegetais preparada" },
            { "latinha_refri", "lata de refrigerante" },
            { "Latinha_refri", "lata de refrigerante" },
            { "leaf-spinach", "folhas de espinafre" },
            { "leek", "alho-poró" },
            { "lemon", "limão" },
            { "lentils", "lentilhas" },
            { "lentils-green-du-puy-du-berry", "lentilhas verdes du Puy du Berry" },
            { "lettuce", "alface" },
            { "light-beer", "cerveja light" },
            { "linseeds", "sementes de linhaça" },
            { "lye-pretzel-soft", "pretzel macio" },

            // M
            { "m-m-s", "M&M's" },
            { "macaroon", "macaron" },
            { "mandarine", "tangerina" },
            { "mango", "manga" },
            { "mango-dried", "manga seca" },
            { "maple-syrup-concentrate", "xarope de bordo concentrado" },
            { "margarine", "margarina" },
            { "mashed-potatoes-prepared-with-full-fat-milk-with-butter", "purê de batatas preparado com leite integral e manteiga" },
            { "mayonnaise", "maionese" },
            { "meat", "carne" },
            { "meat-balls", "almôndegas" },
            { "meat-terrine-pate", "patê/terrino de carne" },
            { "meatloaf", "panqueca de carne" },
            { "melon", "melão" },
            { "meringue", "merengue" },
            { "milk", "leite" },
            { "milk_shake", "milk-shake" },
            { "milk-chocolate", "chocolate ao leite" },
            { "milk-chocolate-with-hazelnuts", "chocolate ao leite com avelãs" },
            { "milkshake", "milk-shake" },
            { "mixed-nuts", "mix de castanhas" },
            { "mixed-salad-chopped-without-sauce", "salada mista picada sem molho" },
            { "mixed-vegetables", "legumes mistos" },
            { "mozzarella", "mussarela" },
            { "muesli", "muesli" },
            { "muffin", "muffin" },
            { "mushroom", "cogumelo" },
            { "mushroom-average-stewed-without-addition-of-fat-without-addition-of-salt", "cogumelo cozido sem adição de gordura e sal" },
            { "mushrooms", "cogumelos" },
            { "mustard", "mostarda" },

            // N
            { "nectarine", "nectarina" },
            { "noodles", "macarrão instantâneo" },

            // O
            { "oil-vinegar-salad-dressing", "molho de azeite e vinagre para salada" },
            { "okra", "quiabo" },
            { "olives", "azeitonas" },
            { "omelette-plain", "omelete simples" },
            { "onion", "cebola" },
            { "orange", "laranja" },
            { "other ingredients", "outros ingredientes" },
            { "oyster mushroom", "cogumelo oyster" },

            // P
            { "pao_puma", "pão puma" },
            { "parmesan", "parmesão" },
            { "parsley", "salsinha" },
            { "pasta", "massa" },
            { "pasta-linguini-parpadelle-tagliatelle", "massa linguini/parpadelle/tagliatelle" },
            { "pasta-noodles", "macarrão" },
            { "pasta-penne", "penne" },
            { "pasta-ravioli-stuffing", "recheio de ravióli" },
            { "pasta-spaghetti", "espaguete" },
            { "pasta-twist", "parafuso de massa" },
            { "peach", "pêssego" },
            { "peanut", "amendoim" },
            { "peanut-butter", "manteiga de amendoim" },
            { "pear", "pera" },
            { "peas", "ervilhas" },
            { "pecan-nut", "noz-pecã" },
            { "pepper", "pimenta" },
            { "philadelphia", "cream cheese Philadelphia" },
            { "pie", "torta" },
            { "pie-plum-baked-with-cake-dough", "torta de ameixa assada com massa de bolo" },
            { "pine-nuts", "pinoli" },
            { "pineapple", "abacaxi" },
            { "pistachio", "pistache" },
            { "pizza", "pizza" },
            { "pizza-margherita-baked", "pizza margherita assada" },
            { "pizza-with-ham-with-mushrooms-baked", "pizza com presunto e cogumelos assada" },
            { "pizza-with-vegetables-baked", "pizza com vegetais assada" },
            { "plums", "ameixas" },
            { "pomegranate", "romã" },
            { "popcorn", "pipoca" },
            { "popcorn-salted", "pipoca salgada" },
            { "pork", "carne de porco" },
            { "pork-roast", "assar carne de porco" },
            { "potato", "batata" },
            { "potato-gnocchi", "gnocchi de batata" },
            { "potato-salad-with-mayonnaise-yogurt-dressing", "salada de batata com molho de maionese e iogurte" },
            { "potatoes-au-gratin-dauphinois-prepared", "batatas gratinadas dauphinois preparadas" },
            { "potatoes-steamed", "batatas cozidas no vapor" },
            { "praline", "praliné" },
            { "processed-cheese", "queijo processado" },
            { "prosecco", "prosecco" },
            { "pudding", "pudim" },
            { "pumpkin", "abóbora" },
            { "pumpkin-seeds", "sementes de abóbora" },

            // Q
            { "quiche-with-cheese-baked-with-puff-pastry", "quiche de queijo assada com massa folhada" },
            { "quinoa", "quinoa" },

            // R
            { "rape", "nabo" },
            { "raspberries", "framboesas" },
            { "raspberry", "framboesa" },
            { "ratatouille", "ratatouille" },
            { "red beans", "feijão vermelho" },
            { "red-radish", "rabanete vermelho" },
            { "Refrigerante", "refrigerante" },
            { "rice", "arroz" },
            { "rice-basmati", "arroz basmati" },
            { "rice-noodles-vermicelli", "macarrão de arroz/vermicelli" },
            { "rice-waffels", "waffles de arroz" },
            { "rice-whole-grain", "arroz integral" },
            { "rice-wild", "arroz selvagem" },
            { "risotto-without-cheese-cooked", "risoto sem queijo cozido" },
            { "ristretto-with-caffeine", "ristretto com cafeína" },
            { "roll-of-half-white-or-white-flour-with-large-void", "pão de farinha semi-branca ou branca com grandes alvéolos" },
            { "roll-with-pieces-of-chocolate", "pão com pedaços de chocolate" },
            { "romanesco", "brócolis romano" },
            { "rosti", "rosti" },
            { "rusk-wholemeal", "torrada integral" },

            // S
            { "salad", "salada" },
            { "salad-dressing", "molho para salada" },
            { "salad-lambs-ear", "salada orelha de cordeiro" },
            { "salad-leaf-salad-green", "folhas de salada verde" },
            { "salad-rocket", "rúcula" },
            { "salami", "salame" },
            { "salmon", "salmão" },
            { "salmon-smoked", "salmão defumado" },
            { "sauce", "molho" },
            { "sauce-cream", "molho de creme" },
            { "sauce-curry", "molho curry" },
            { "sauce-mushroom", "molho de cogumelos" },
            { "sauce-pesto", "molho pesto" },
            { "sauce-roast", "molho de assado" },
            { "sauce-savoury", "molho salgado" },
            { "sauce-sweet-salted-asian", "molho doce e salgado asiático" },
            { "sauce-sweet-sour", "molho agridoce" },
            { "sauerkraut", "chucrute" },
            { "sausage", "salsicha" },
            { "savoury-puff-pastry-stick", "bastão de massa folhada salgado" },
            { "seaweed", "alga marinha" },
            { "sekt", "espumante alemão" },
            { "semi-hard-cheese", "queijo semi-duro" },
            { "sesame-seeds", "sementes de gergelim" },
            { "shellfish", "marisco" },
            { "shiitake", "shiitake" },
            { "shoots", "brotos" },
            { "shrimp", "camarão" },
            { "shrimp-boiled", "camarão cozido" },
            { "shrimp-prawn-large", "camarão grande" },
            { "snow peas", "ervilha-torta" },
            { "soft-cheese", "queijo macio" },
            { "sorbet", "sorbet" },
            { "soup", "sopa" },
            { "soup-cream-of-vegetables", "sopa cremosa de legumes" },
            { "soup-miso", "sopa missô" },
            { "soup-pumpkin", "sopa de abóbora" },
            { "soup-tomato", "sopa de tomate" },
            { "soup-vegetable", "sopa de legumes" },
            { "sour-cream", "creme azedo" },
            { "soy", "soja" },
            { "soya-drink-soy-milk", "bebida de soja/leite de soja" },
            { "soya-yaourt-yahourt-yogourt-ou-yoghourt", "iogurte de soja" },
            { "spaetzle", "spaetzle" },
            { "spinach-raw", "espinafre cru" },
            { "spinach-steamed-without-addition-of-salt", "espinafre cozido no vapor sem sal" },
            { "spring onion", "cebolinha" },
            { "spring-onion-scallion", "cebolinha/cebola verde" },
            { "spring-roll-fried", "rolinho primavera frito" },
            { "steak", "bife" },
            { "strawberries", "morangos" },
            { "strawberry", "morango" },
            { "sugar-melon", "melão açúcar" },
            { "sun-dried-tomatoe", "tomate seco" },
            { "sunflower-seeds", "sementes de girassol" },
            { "sushi", "sushi" },
            { "sweet-corn-canned", "milho doce enlatado" },
            { "sweet-potato", "batata-doce" },
            { "sweets-candies", "doces/balas" },
            { "swiss-chard", "acelga" },
            { "syrup-diluted-ready-to-drink", "xarope diluído pronto para beber" },

            // T
            { "tartar-sauce", "molho tártaro" },
            { "tea", "chá" },
            { "tea-green", "chá verde" },
            { "tea-peppermint", "chá de hortelã-pimenta" },
            { "tea-rooibos", "chá rooibos" },
            { "tete-de-moine", "tête de moine" },
            { "tiramisu", "tiramisu" },
            { "tofu", "tofu" },
            { "tomato", "tomate" },
            { "tomato-raw", "tomate cru" },
            { "tomato-sauce", "molho de tomate" },
            { "tomato-stewed-without-addition-of-fat-without-addition-of-salt", "tomate cozido sem adição de gordura e sal" },
            { "tomme", "tomme" },
            { "turnover-with-meat-small-meat-pie-empanadas", "pastel de carne pequeno/empanadas" },

            // U
            { "tzatziki", "tzatziki" },
            { "undefined", "indefinido" },

            // V
            { "veal-sausage", "salsicha de vitela" },
            { "vegetable-au-gratin-baked", "legumes gratinados assados" },
            { "vegetable-mix-peas-and-carrots", "mix de legumes (ervilhas e cenouras)" },
            { "vegetables", "legumes/vegetais" },
            { "veggie-burger", "hambúrguer veggie" },

            // W
            { "walnut", "noz" },
            { "water", "água" },
            { "water-mineral", "água mineral" },
            { "water-with-lemon-juice", "água com suco de limão" },
            { "watermelon", "melancia" },
            { "watermelon-fresh", "melancia fresca" },
            { "white button mushroom", "cogumelo champignon" },
            { "white radish", "rabanete branco" },
            { "white-bread-with-butter-eggs-and-milk", "pão branco com manteiga, ovos e leite" },
            { "white-coffee-with-caffeine", "café com leite com cafeína" },
            { "wienerli-swiss-sausage", "salsicha suíça wienerli" },
            { "wine", "vinho" },
            { "wine-red", "vinho tinto" },
            { "wine-rose", "vinho rosé" },
            { "wine-white", "vinho branco" },
            { "witloof-chicory", "chicória witloof" },
            { "wonton dumplings", "bolinhos wonton" },

            // Y
            { "yaourt-yahourt-yogourt-ou-yoghourt-natural", "iogurte natural" },

            // Z
            { "zucchini", "abobrinha" }
        };


        private static readonly string[] AlimentosPermitidos = Tradutor.Keys.ToArray();

        public YoloInferenceService()
        {
            var modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Models", "bsfmv1_yolo_final.onnx");

            var options = new YoloOptions
            {
                OnnxModel = modelPath,
                ModelType = ModelType.ObjectDetection,
                Cuda = false, 
                GpuId = 0,
            };

            _yolo = new Yolo(options);
            Console.WriteLine($"[IA] Inicializada com o modelo: {modelPath}");
        }

        public List<string> DetectarAlimentos(byte[] imageBytes)
        {
            var resultadoFinalPT = new List<string>();

            // Criamos o tradutor normalizado uma única vez (Performance)
            var tradutorOtimizado = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in Tradutor)
            {
                // Garante que as chaves do dicionário também estejam sem hifens e limpas
                string chaveLimpa = item.Key.Replace("-", " ").Replace("_", " ").Trim();
                tradutorOtimizado[chaveLimpa] = item.Value;
            }

            try
            {
                if (imageBytes == null || imageBytes.Length == 0) return resultadoFinalPT;
                using var ms = new MemoryStream(imageBytes);
                using var image = SKImage.FromEncodedData(ms);
                if (image == null) return resultadoFinalPT;

                // Rodamos a detecção com confiança equilibrada
                var results = _yolo.RunObjectDetection(image, 0.16); 

                var ordenados = results.OrderByDescending(x => x.Confidence).ToList();

                foreach (var r in ordenados)
                {
                    // --- LIMPEZA AGRESSIVA DE CARACTERES ---
                    // Remove ' (aspas simples), " (aspas duplas), - (hífens), _ (underlines)
                    string labelSanitizada = r.Label.Name
                        .Replace("'", "")
                        .Replace("\"", "")
                        .Replace("-", " ")
                        .Replace("_", " ")
                        .Trim()
                        .ToLower();

                    Console.WriteLine($"[IA RAW] Recebido: [{r.Label.Name}] -> Sanitizado para: [{labelSanitizada}]");

                    // TENTA TRADUZIR
                    if (tradutorOtimizado.TryGetValue(labelSanitizada, out string nomeTraduzido))
                    {
                        if (!resultadoFinalPT.Contains(nomeTraduzido))
                        {
                            resultadoFinalPT.Add(nomeTraduzido);
                        }
                    }
                    else
                    {
                        // SOLUÇÃO PARA O 'NADA':
                        // Se a IA detectou um dos 452 alimentos e você ainda não traduziu ele no dicionário,
                        // vamos ADICIONAR o nome limpo mesmo assim para o sistema não retornar vazio (404).
                        if (!resultadoFinalPT.Contains(labelSanitizada))
                        {
                            resultadoFinalPT.Add(labelSanitizada);
                        }
                        Console.WriteLine($"[IA INFO] '{labelSanitizada}' não tem tradução, mas foi incluído.");
                    }
                }

                return resultadoFinalPT;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[IA ERROR FATAL] {ex.Message}");
                return resultadoFinalPT;
            }
        }
    }
}