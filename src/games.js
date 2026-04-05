/** Benefícios comuns nos planos Free Fire (todas as plataformas). */
export const FREEFIRE_SHARED_PERKS = [
  'Painel VIP',
  'Suporte',
  'Desenvolvedores trabalhando em otimização',
  '3 dias de garantia — se não ficar satisfeito, devolvemos o seu dinheiro',
]

function freefirePlansForPlatform(prefix, productTitle) {
  const durations = [
    { suffix: '7', label: '7 dias', price: 50 },
    { suffix: '30', label: '30 dias', price: 100 },
    { suffix: '90', label: '3 meses', price: 200 },
  ]
  return durations.map(({ suffix, label, price }) => ({
    id: `${prefix}-${suffix}`,
    name: `${productTitle} — ${label}`,
    price,
    periodLabel: label,
    blurb: `Acesso ao painel por ${label}.`,
    perks: FREEFIRE_SHARED_PERKS,
  }))
}

/** Planos Free Fire após escolher plataforma (Android / iOS / PC). */
export const FREEFIRE_PLANS_BY_PLATFORM = {
  android: freefirePlansForPlatform('ff-android', 'Apk Android Painel'),
  ios: freefirePlansForPlatform('ff-ios', 'Painel iOS'),
  pc: freefirePlansForPlatform('ff-pc', 'Painel PC'),
}

export const FREEFIRE_PLATFORMS = [
  { id: 'android', label: 'Android' },
  { id: 'ios', label: 'iOS' },
  { id: 'pc', label: 'PC' },
]

const VALORANT_SHARED_PERKS_TAIL = [
  'Suporte',
  'Desenvolvedores trabalhando em otimização',
  '3 dias de garantia — se não ficar satisfeito, devolvemos o seu dinheiro',
]

const VALORANT_PERKS_AIMBOT = ['Aimbot VIP', ...VALORANT_SHARED_PERKS_TAIL]
const VALORANT_PERKS_AIMBOT_ESP = ['Aimbot e ESP VIP', ...VALORANT_SHARED_PERKS_TAIL]

function valorantPlansForMode(prefix, productTitle, perks) {
  const durations = [
    { suffix: '7', label: '7 dias', price: 50 },
    { suffix: '30', label: '30 dias', price: 100 },
    { suffix: '90', label: '3 meses', price: 200 },
  ]
  return durations.map(({ suffix, label, price }) => ({
    id: `${prefix}-${suffix}`,
    name: `${productTitle} — ${label}`,
    price,
    periodLabel: label,
    blurb: `Acesso ao kit por ${label}.`,
    perks,
  }))
}

/** Valorant: após escolher Aimbot ou Aimbot + ESP. */
export const VALORANT_PLANS_BY_MODE = {
  aimbot: valorantPlansForMode('val-aimbot', 'Aimbot', VALORANT_PERKS_AIMBOT),
  aimbotEsp: valorantPlansForMode('val-aimbot-esp', 'Aimbot + ESP', VALORANT_PERKS_AIMBOT_ESP),
}

export const VALORANT_MODES = [
  { id: 'aimbot', label: 'Aimbot' },
  { id: 'aimbotEsp', label: 'Aimbot + ESP' },
]

const AIMBOT_PAINEL_PERKS_AIMBOT = ['Aimbot VIP', ...VALORANT_SHARED_PERKS_TAIL]
const AIMBOT_PAINEL_PERKS_PAINEL = ['Painel VIP', ...VALORANT_SHARED_PERKS_TAIL]

function aimbotPainelPlansByGame(prefix) {
  return {
    aimbot: valorantPlansForMode(`${prefix}-aimbot`, 'Aimbot', AIMBOT_PAINEL_PERKS_AIMBOT),
    painel: valorantPlansForMode(`${prefix}-painel`, 'Painel', AIMBOT_PAINEL_PERKS_PAINEL),
  }
}

/** Jogos com fluxo Aimbot / Painel + durações (7 / 30 / 90 dias). */
export const AIMBOT_PAINEL_GAMES = {
  cs2: aimbotPainelPlansByGame('cs2'),
  fortnite: aimbotPainelPlansByGame('fn'),
  apex: aimbotPainelPlansByGame('apex'),
  warzone: aimbotPainelPlansByGame('wz'),
}

export const AIMBOT_PAINEL_GAME_IDS = Object.keys(AIMBOT_PAINEL_GAMES)

export const AIMBOT_PAINEL_MODES = [
  { id: 'aimbot', label: 'Aimbot' },
  { id: 'painel', label: 'Painel' },
]

/** Planos com períodos e preços custom (painel / mapa etc.). */
function customPeriodPlans(prefix, productTitle, tiers, perks, blurbFn = (label) => `Acesso por ${label}.`) {
  return tiers.map(({ suffix, label, price }) => ({
    id: `${prefix}-${suffix}`,
    name: `${productTitle} — ${label}`,
    price,
    periodLabel: label,
    blurb: blurbFn(label),
    perks,
  }))
}

const PANEL_STANDARD_PERKS = [
  'Painel VIP',
  'Suporte',
  'Desenvolvedores trabalhando em otimização',
  '3 dias de garantia — se não ficar satisfeito, devolvemos o seu dinheiro',
]

const ROBLOX_PERKS = [
  'Painel multi função VIP',
  'Suporte',
  'Desenvolvedores trabalhando em otimização',
  '3 dias de garantia — se não ficar satisfeito, devolvemos o seu dinheiro',
]

const POGO_PERKS = [
  'Mapa + teleporte 100% anti ban',
  'Suporte',
  'Desenvolvedores trabalhando em otimização',
  '3 dias de garantia — se não ficar satisfeito, devolvemos o seu dinheiro',
]

const TIER_7_1M_2M = [
  { suffix: '7', label: '7 dias', price: 39.9 },
  { suffix: '30', label: '1 mês', price: 59.9 },
  { suffix: '60', label: '2 meses', price: 99.9 },
]

const BRAWL_TIERS = [
  { suffix: '7', label: '7 dias', price: 39.9 },
  { suffix: '30', label: '1 mês', price: 59.9 },
  { suffix: '90', label: '3 meses', price: 149.9 },
]

function brawlPlansForPlatform(platformId, platformLabel) {
  const title = `Painel (${platformLabel})`
  return customPeriodPlans(`brawl-${platformId}-painel`, title, BRAWL_TIERS, PANEL_STANDARD_PERKS)
}

/** Brawl Stars: PC ou Mobile → só Painel (7 dias / 1 mês / 3 meses). */
export const BRAWLSTARS_PLANS_BY_PLATFORM = {
  pc: brawlPlansForPlatform('pc', 'PC'),
  mobile: brawlPlansForPlatform('mobile', 'Mobile'),
}

export const BRAWLSTARS_PLATFORMS = [
  { id: 'pc', label: 'PC' },
  { id: 'mobile', label: 'Mobile' },
]

/** Roblox: direto ao painel — sem etapa extra. */
export const ROBLOX_PLANS = customPeriodPlans(
  'roblox',
  'Painel multi função',
  TIER_7_1M_2M,
  ROBLOX_PERKS,
)

/** Pokémon GO: Android ou iOS → mesmo produto e preços. */
export const POKEMONGO_PLANS_BY_PLATFORM = {
  android: customPeriodPlans(
    'pogo-android',
    'Mapa + Teleporte 100% anti ban',
    TIER_7_1M_2M,
    POGO_PERKS,
  ),
  ios: customPeriodPlans(
    'pogo-ios',
    'Mapa + Teleporte 100% anti ban',
    TIER_7_1M_2M,
    POGO_PERKS,
  ),
}

export const POKEMONGO_PLATFORMS = [
  { id: 'android', label: 'Android' },
  { id: 'ios', label: 'iOS' },
]

const CLASHROYALE_PERKS = [
  'Elixir infinito VIP',
  'Suporte',
  'Desenvolvedores trabalhando em otimização',
  '3 dias de garantia — se não ficar satisfeito, devolvemos o seu dinheiro',
]

const CLASHROYALE_TIERS = [
  { suffix: '7', label: '7 dias', price: 79.9 },
  { suffix: '30', label: '1 mês', price: 119.9 },
  { suffix: '60', label: '2 meses', price: 199.9 },
]

/** Clash Royale: só Elixir infinito — sem etapa extra. */
export const CLASHROYALE_PLANS = customPeriodPlans(
  'clash',
  'Elixir infinito',
  CLASHROYALE_TIERS,
  CLASHROYALE_PERKS,
)

/** Dados fictícios para estudo — ajuste depois. */
export const PLANS = [
  {
    id: 'starter',
    name: 'Starter',
    price: 29.9,
    blurb: 'Ideal para testar em partidas casuais.',
    perks: [
      'Atualizações semanais',
      'Suporte por e-mail',
      '1 dispositivo',
      '3 dias de garantia — se não ficar satisfeito, devolvemos o seu dinheiro',
    ],
  },
  {
    id: 'pro',
    name: 'Pro',
    price: 59.9,
    blurb: 'O equilíbrio entre performance e preço.',
    perks: [
      'Prioridade nas filas de update',
      'Discord privado',
      'Até 2 dispositivos',
      '3 dias de garantia — se não ficar satisfeito, devolvemos o seu dinheiro',
    ],
  },
  {
    id: 'elite',
    name: 'Elite',
    price: 99.9,
    blurb: 'Máximo de recursos e estabilidade.',
    perks: [
      'Patches em tempo real',
      'Suporte VIP 24h',
      'Até 3 dispositivos',
      'Beta antecipado',
      '3 dias de garantia — se não ficar satisfeito, devolvemos o seu dinheiro',
    ],
  },
]

export const GAMES = [
  {
    id: 'freefire',
    name: 'Free Fire',
    image: '/imagens/Perfil_FreeFire.jpg',
    tag: 'Battle royale mobile',
  },
  {
    id: 'valorant',
    name: 'Valorant',
    image: '/imagens/Perfil_Valorant.png',
    tag: 'FPS tático 5v5',
  },
  {
    id: 'cs2',
    name: 'Counter-Strike 2',
    image: '/imagens/Perfil_cs2.jpg',
    tag: 'FPS competitivo',
  },
  {
    id: 'warzone',
    name: 'Warzone',
    image: '/imagens/Perfil_Wazone.jpg',
    tag: 'Battle royale',
  },
  {
    id: 'fortnite',
    name: 'Fortnite',
    image: '/imagens/Perfil_Fortinete.jpg',
    tag: 'Battle royale / PC',
  },
  {
    id: 'apex',
    name: 'Apex Legends',
    image: '/imagens/Perfil_Apex.jpg',
    tag: 'Hero shooter BR',
  },
  {
    id: 'roblox',
    name: 'Roblox',
    image: '/imagens/Perfil_Roblox.png',
    tag: 'Plataforma / diversos',
  },
  {
    id: 'brawlstars',
    name: 'Brawl Stars',
    image: '/imagens/Perfil_BrawlStarts.jpg',
    tag: 'Arena mobile',
  },
  {
    id: 'clashroyale',
    name: 'Clash Royale',
    image: '/imagens/Perfil_ClashRoyal.jpg',
    tag: 'Estratégia em tempo real',
  },
  {
    id: 'pokemongo',
    name: 'Pokémon GO',
    image: '/imagens/Perfil_PokemonGO.jpg',
    tag: 'AR / coleta',
  },
]
