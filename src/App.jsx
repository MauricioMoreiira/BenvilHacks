import { useCallback, useEffect, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import {
  AIMBOT_PAINEL_GAME_IDS,
  AIMBOT_PAINEL_GAMES,
  AIMBOT_PAINEL_MODES,
  BRAWLSTARS_PLANS_BY_PLATFORM,
  BRAWLSTARS_PLATFORMS,
  CLASHROYALE_PLANS,
  FREEFIRE_PLANS_1REAL_BY_PLATFORM,
  FREEFIRE_PLANS_BY_PLATFORM,
  FREEFIRE_PLATFORMS,
  GAMES,
  PLANS,
  POKEMONGO_PLANS_BY_PLATFORM,
  POKEMONGO_PLATFORMS,
  ROBLOX_PLANS,
  VALORANT_MODES,
  VALORANT_PLANS_BY_MODE,
} from './games'
import { apiPath } from './api'
import { transformContactEmailPrank } from './contactEmailPrank'
import { CheckoutPixPanel } from './CheckoutPixPanel'
import './App.css'

const LOGO_SRC = '/imagens/Logo%20Empresa.png'

/** Lê retorno do Mercado Pago na URL e limpa a query (executar só no primeiro render no cliente). */
function readMercadoPagoReturn() {
  if (typeof window === 'undefined') {
    return { initialOverlay: null, verify: null }
  }
  const params = new URLSearchParams(window.location.search)
  const mp = params.get('mp')
  if (!mp) {
    return { initialOverlay: null, verify: null }
  }

  const paymentId = params.get('payment_id')
  const orderId = params.get('external_reference')
  const u = new URL(window.location.href)
  u.search = ''
  window.history.replaceState({}, '', `${u.pathname}${u.hash}`)

  if (mp === 'failure') {
    return {
      initialOverlay: {
        variant: 'failure',
        title: 'Pagamento não concluído',
        text: 'Você cancelou ou o pagamento foi recusado. Nenhuma cobrança foi efetuada. Quando quiser, abra de novo os planos e tente outra vez.',
      },
      verify: null,
    }
  }

  if (mp === 'pending') {
    return {
      initialOverlay: {
        variant: 'pending',
        title: 'Pagamento em análise',
        text: 'O Mercado Pago está processando. Assim que for aprovado, você receberá a confirmação e poderá voltar aqui pelo link do comprovante ou falar com o suporte.',
      },
      verify: null,
    }
  }

  if (mp !== 'success') {
    return { initialOverlay: null, verify: null }
  }

  if (!paymentId || !orderId) {
    return {
      initialOverlay: {
        variant: 'failure',
        title: 'Não foi possível confirmar',
        text: 'Faltam dados do retorno do pagamento. Se você já pagou, guarde o comprovante e fale com o suporte.',
      },
      verify: null,
    }
  }

  return {
    initialOverlay: {
      variant: 'verifying',
      title: 'Confirmando pagamento…',
      text: 'Só um instante enquanto validamos com o Mercado Pago.',
    },
    verify: { paymentId, orderId },
  }
}

let mercadoPagoReturnMemo = null
function getMercadoPagoReturnOnce() {
  if (mercadoPagoReturnMemo) return mercadoPagoReturnMemo
  mercadoPagoReturnMemo = readMercadoPagoReturn()
  return mercadoPagoReturnMemo
}

let mercadoPagoVerifyEffectStarted = false

function formatBRL(value) {
  return new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(value)
}

function maskCpfDisplay(raw) {
  const d = String(raw).replace(/\D/g, '')
  if (d.length !== 11) return ''
  return `***.***.*${d.slice(7, 9)}-${d.slice(9)}`
}

export default function App() {
  const [pathname, setPathname] = useState(() =>
    typeof window !== 'undefined' ? window.location.pathname : '/',
  )
  const [modalGameId, setModalGameId] = useState(null)
  const [planId, setPlanId] = useState(PLANS[1].id)
  const [freeFirePlatform, setFreeFirePlatform] = useState(null)
  const [valorantMode, setValorantMode] = useState(null)
  const [aimbotPainelMode, setAimbotPainelMode] = useState(null)
  const [brawlPlatform, setBrawlPlatform] = useState(null)
  const [pogoPlatform, setPogoPlatform] = useState(null)
  const [showCheckoutForm, setShowCheckoutForm] = useState(false)
  const [checkoutEmail, setCheckoutEmail] = useState('')
  const [checkoutPhone, setCheckoutPhone] = useState('')
  const [checkoutCpf, setCheckoutCpf] = useState('')
  const [checkoutError, setCheckoutError] = useState(null)
  const [checkoutLoading, setCheckoutLoading] = useState(false)
  const [checkoutSession, setCheckoutSession] = useState(null)
  const [checkoutPaid, setCheckoutPaid] = useState(null)
  const mpOnce = getMercadoPagoReturnOnce()
  const [paymentOverlay, setPaymentOverlay] = useState(mpOnce.initialOverlay)
  const verifyPaymentRef = useRef(mpOnce.verify)
  const modalRef = useRef(null)
  const lastFocusRef = useRef(null)

  const resetCheckoutFlow = useCallback(() => {
    setShowCheckoutForm(false)
    setCheckoutError(null)
    setCheckoutLoading(false)
    setCheckoutSession(null)
    setCheckoutPaid(null)
    setCheckoutEmail('')
    setCheckoutPhone('')
    setCheckoutCpf('')
  }, [])

  const openModal = (gameId) => {
    lastFocusRef.current = document.activeElement
    resetCheckoutFlow()
    setFreeFirePlatform(null)
    setValorantMode(null)
    setAimbotPainelMode(null)
    setBrawlPlatform(null)
    setPogoPlatform(null)
    if (gameId === 'freefire') {
      const first = FREEFIRE_PLANS_BY_PLATFORM.android[0]
      setPlanId(first.id)
    } else if (gameId === 'valorant') {
      setPlanId(VALORANT_PLANS_BY_MODE.aimbot[0].id)
    } else if (AIMBOT_PAINEL_GAME_IDS.includes(gameId)) {
      setPlanId(AIMBOT_PAINEL_GAMES[gameId].aimbot[0].id)
    } else if (gameId === 'brawlstars') {
      setPlanId(BRAWLSTARS_PLANS_BY_PLATFORM.pc[0].id)
    } else if (gameId === 'roblox') {
      setPlanId(ROBLOX_PLANS[0].id)
    } else if (gameId === 'pokemongo') {
      setPlanId(POKEMONGO_PLANS_BY_PLATFORM.android[0].id)
    } else if (gameId === 'clashroyale') {
      setPlanId(CLASHROYALE_PLANS[0].id)
    } else {
      setPlanId(PLANS[1].id)
    }
    setModalGameId(gameId)
  }

  const normalizedPath = (pathname.replace(/\/$/, '') || '/')
  const isTestOneRealRoute = normalizedPath === '/teste/tudo1real'

  const closeModal = useCallback(() => {
    if (isTestOneRealRoute) {
      window.history.pushState({}, '', '/')
      setPathname('/')
      resetCheckoutFlow()
      setFreeFirePlatform(null)
      lastFocusRef.current?.focus?.()
      return
    }
    setModalGameId(null)
    resetCheckoutFlow()
    lastFocusRef.current?.focus?.()
  }, [resetCheckoutFlow, isTestOneRealRoute])

  const game = isTestOneRealRoute
    ? (GAMES.find((g) => g.id === 'freefire') ?? null)
    : modalGameId
      ? GAMES.find((g) => g.id === modalGameId)
      : null
  const isFreeFire = game?.id === 'freefire'
  const isValorant = game?.id === 'valorant'
  const isAimbotPainelGame = !!(game && AIMBOT_PAINEL_GAME_IDS.includes(game.id))
  const isBrawlStars = game?.id === 'brawlstars'
  const isRoblox = game?.id === 'roblox'
  const isPokemonGo = game?.id === 'pokemongo'
  const isClashRoyale = game?.id === 'clashroyale'
  const freeFirePlansSource = isTestOneRealRoute ? FREEFIRE_PLANS_1REAL_BY_PLATFORM : FREEFIRE_PLANS_BY_PLATFORM
  const freeFirePlans =
    freeFirePlatform && isFreeFire ? freeFirePlansSource[freeFirePlatform] : null
  const valorantPlans =
    valorantMode && isValorant ? VALORANT_PLANS_BY_MODE[valorantMode] : null
  const aimPainelPlans =
    aimbotPainelMode && isAimbotPainelGame
      ? AIMBOT_PAINEL_GAMES[game.id][aimbotPainelMode]
      : null
  const brawlPlans =
    brawlPlatform && isBrawlStars ? BRAWLSTARS_PLANS_BY_PLATFORM[brawlPlatform] : null
  const pogoPlans =
    pogoPlatform && isPokemonGo ? POKEMONGO_PLANS_BY_PLATFORM[pogoPlatform] : null
  const catalogPlans =
    isFreeFire && freeFirePlans
      ? freeFirePlans
      : isValorant && valorantPlans
        ? valorantPlans
        : isAimbotPainelGame && aimPainelPlans
          ? aimPainelPlans
          : isBrawlStars && brawlPlans
            ? brawlPlans
            : isRoblox
              ? ROBLOX_PLANS
              : isClashRoyale
                ? CLASHROYALE_PLANS
                : isPokemonGo && pogoPlans
                  ? pogoPlans
                  : PLANS
  const selectedPlan =
    catalogPlans.find((p) => p.id === planId) ?? catalogPlans[0] ?? PLANS[0]

  const selectFreeFirePlatform = (platformId) => {
    const list = freeFirePlansSource[platformId]
    setFreeFirePlatform(platformId)
    setPlanId(list[0].id)
  }

  const selectValorantMode = (modeId) => {
    const list = VALORANT_PLANS_BY_MODE[modeId]
    setValorantMode(modeId)
    setPlanId(list[0].id)
  }

  const selectAimbotPainelMode = (modeId) => {
    if (!game) return
    const list = AIMBOT_PAINEL_GAMES[game.id][modeId]
    setAimbotPainelMode(modeId)
    setPlanId(list[0].id)
  }

  const selectBrawlPlatform = (platformId) => {
    const list = BRAWLSTARS_PLANS_BY_PLATFORM[platformId]
    setBrawlPlatform(platformId)
    setPlanId(list[0].id)
  }

  const selectPogoPlatform = (platformId) => {
    const list = POKEMONGO_PLANS_BY_PLATFORM[platformId]
    setPogoPlatform(platformId)
    setPlanId(list[0].id)
  }

  const showFirstStepFreeFire = isFreeFire && !freeFirePlatform
  const showFirstStepValorant = isValorant && !valorantMode
  const showFirstStepAimPainel = isAimbotPainelGame && !aimbotPainelMode
  const showFirstStepBrawl = isBrawlStars && !brawlPlatform
  const showFirstStepPogo = isPokemonGo && !pogoPlatform

  const showDurationIntro =
    (isFreeFire && !!freeFirePlatform) ||
    (isValorant && !!valorantMode) ||
    (isAimbotPainelGame && !!aimbotPainelMode) ||
    (isBrawlStars && !!brawlPlatform) ||
    isRoblox ||
    isClashRoyale ||
    (isPokemonGo && !!pogoPlatform)

  const modalIntroMain = (() => {
    if (showFirstStepFreeFire) return 'Primeiro, escolha se você joga no Android, iOS ou PC.'
    if (showFirstStepValorant) return 'Primeiro, escolha entre Aimbot apenas ou Aimbot + ESP.'
    if (showFirstStepAimPainel) {
      return game.id === 'fortnite'
        ? 'Escolha entre Aimbot ou Painel. (Somente versão para PC.)'
        : 'Primeiro, escolha entre Aimbot ou Painel.'
    }
    if (showFirstStepBrawl) return 'Primeiro, escolha se você joga na versão para PC ou Mobile.'
    if (showFirstStepPogo) return 'Primeiro, escolha se você usa Android ou iOS.'
    if (showDurationIntro) return 'Escolha a duração do plano. O total aparece abaixo.'
    return 'Escolha um plano mensal. O total aparece ao final.'
  })()

  const showPlanCheckout =
    (!isFreeFire || freeFirePlatform) &&
    (!isValorant || valorantMode) &&
    (!isAimbotPainelGame || aimbotPainelMode) &&
    (!isBrawlStars || brawlPlatform) &&
    (!isPokemonGo || pogoPlatform)

  const checkoutSubflow = Boolean(showCheckoutForm || checkoutSession || checkoutPaid)

  useEffect(() => {
    const onPop = () => setPathname(window.location.pathname)
    window.addEventListener('popstate', onPop)
    return () => window.removeEventListener('popstate', onPop)
  }, [])

  useEffect(() => {
    if (!modalGameId && !isTestOneRealRoute) return
    const onKey = (e) => {
      if (e.key === 'Escape') closeModal()
    }
    document.addEventListener('keydown', onKey)
    document.body.style.overflow = 'hidden'
    return () => {
      document.removeEventListener('keydown', onKey)
      document.body.style.overflow = ''
    }
  }, [modalGameId, isTestOneRealRoute, closeModal])

  useEffect(() => {
    if ((modalGameId || isTestOneRealRoute) && modalRef.current) {
      const closeBtn = modalRef.current.querySelector('.modal__close')
      closeBtn?.focus()
    }
  }, [modalGameId, isTestOneRealRoute])

  useEffect(() => {
    modalRef.current?.scrollTo?.(0, 0)
  }, [modalGameId, isTestOneRealRoute, showCheckoutForm, checkoutSession, checkoutPaid])

  useEffect(() => {
    const v = verifyPaymentRef.current
    if (!v || mercadoPagoVerifyEffectStarted) return
    mercadoPagoVerifyEffectStarted = true
    let cancelled = false
    ;(async () => {
      try {
        const res = await fetch(apiPath('/api/checkout/verify'), {
          method: 'POST',
          headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
          body: JSON.stringify({ orderId: v.orderId, paymentId: v.paymentId }),
        })
        const data = await res.json().catch(() => ({}))
        if (cancelled) return
        if (!res.ok) {
          setPaymentOverlay({
            variant: 'failure',
            title: 'Erro ao confirmar',
            text: data?.message ?? 'Tente de novo em instantes.',
          })
          return
        }
        if (data.ok && data.licenseKey) {
          setPaymentOverlay({
            variant: 'success',
            title: 'Parabéns pela compra!',
            text:
              'Seu pagamento foi confirmado. O e-mail com link de download e instruções você vai receber em até 24 horas no endereço cadastrado na compra.',
            licenseKey: data.licenseKey,
            email: data.email ?? '',
          })
          return
        }
        setPaymentOverlay({
          variant: 'failure',
          title: 'Ainda não confirmado',
          text: data?.message ?? 'Aguarde ou verifique no Mercado Pago.',
        })
      } catch {
        if (!cancelled) {
          setPaymentOverlay({
            variant: 'failure',
            title: 'Erro de conexão',
            text: 'Não foi possível falar com o servidor. Tente de novo.',
          })
        }
      }
    })()
    return () => {
      cancelled = true
    }
  }, [])

  const submitCheckout = async (e) => {
    e.preventDefault()
    if (!game) return
    setCheckoutError(null)
    const email = checkoutEmail.trim()
    const phone = checkoutPhone.trim()
    if (!email || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
      setCheckoutError('Informe um e-mail válido.')
      return
    }
    if (phone.length < 8) {
      setCheckoutError('Informe um telefone com DDD para contato.')
      return
    }
    const cpfDigits = checkoutCpf.replace(/\D/g, '')
    if (cpfDigits.length !== 11) {
      setCheckoutError('Informe um CPF válido (11 dígitos).')
      return
    }
    setCheckoutLoading(true)
    const sessionUrl = apiPath('/api/checkout/session')
    try {
      const res = await fetch(sessionUrl, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
        body: JSON.stringify({
          gameId: game.id,
          planId: selectedPlan.id,
          email,
          phone,
          cpf: checkoutCpf.trim(),
        }),
      })
      const rawText = await res.text()
      let data = {}
      try {
        data = rawText ? JSON.parse(rawText) : {}
      } catch (parseErr) {
        console.error(
          '[checkout/session] corpo não é JSON\n' +
            JSON.stringify(
              {
                sessionUrl,
                status: res.status,
                statusText: res.statusText,
                VITE_API_URL: import.meta.env.VITE_API_URL ?? '',
                rawText: rawText.slice(0, 8000),
                parseErr: String(parseErr),
              },
              null,
              2,
            ),
        )
      }
      if (!res.ok) {
        console.error(
          '[checkout/session] HTTP erro\n' +
            JSON.stringify(
              {
                sessionUrl,
                status: res.status,
                statusText: res.statusText,
                VITE_API_URL: import.meta.env.VITE_API_URL ?? '',
                parsed: data,
                rawText: rawText.slice(0, 8000),
              },
              null,
              2,
            ),
        )
        let errMsg = data?.error?.trim() || 'Não foi possível iniciar o checkout.'
        if (res.status === 404 && !data?.error) {
          errMsg =
            'API não encontrada (404). O Vite só encaminha /api no dev/preview com proxy; suba a API em http://localhost:5261 e reinicie o front (npm run dev ou npm run preview).'
        }
        const det = typeof data?.details === 'string' ? data.details.trim() : ''
        setCheckoutError(det ? `${errMsg}\n\n${det}` : errMsg)
        setCheckoutLoading(false)
        return
      }
      if (data.orderId && data.publicKey && typeof data.amount === 'number') {
        setCheckoutSession({
          orderId: data.orderId,
          amount: data.amount,
          publicKey: data.publicKey,
          itemTitle: data.itemTitle ?? '',
          mercadoPagoPixSandboxMode: data.mercadoPagoPixSandboxMode ?? '',
          mercadoPagoPixEnvironmentHints: Array.isArray(data.mercadoPagoPixEnvironmentHints)
            ? data.mercadoPagoPixEnvironmentHints
            : [],
          mercadoPagoAccessTokenKind: data.mercadoPagoAccessTokenKind ?? '',
          mercadoPagoPixPayerEmailMustEndWith: data.mercadoPagoPixPayerEmailMustEndWith ?? '',
        })
        setCheckoutError(null)
        setCheckoutLoading(false)
        return
      }
      console.error(
        '[checkout/session] JSON OK mas faltam orderId/publicKey/amount\n' +
          JSON.stringify({ parsed: data, rawText: rawText.slice(0, 4000) }, null, 2),
      )
      setCheckoutError('Resposta inválida do servidor.')
    } catch (err) {
      console.error(
        '[checkout/session] exceção (rede ou fetch)\n' +
          JSON.stringify({ sessionUrl, err: err instanceof Error ? err.message : String(err) }, null, 2),
      )
      setCheckoutError('Falha de rede. Tente de novo.')
    }
    setCheckoutLoading(false)
  }

  const closePaymentOverlay = () => setPaymentOverlay(null)

  return (
    <div className="page">
      <div className="grid-bg" aria-hidden="true" />

      {!isTestOneRealRoute ? (
        <>
      <header className="header">
        <a href="#top" className="brand">
          <img src={LOGO_SRC} alt="Benvil Hacks" className="brand__logo" width={48} height={48} />
          <span className="brand__text">Benvil Hacks</span>
        </a>
        <nav className="nav" aria-label="Principal">
          <a href="#jogos">Catálogo</a>
          <a href="#stats">Números</a>
          <a href="#contato">Contato</a>
        </nav>
        <a href="#jogos" className="btn btn--sm btn--primary">
          Ver planos
        </a>
      </header>

      <main id="top">
        <section className="hero">
          <div className="hero__glow" aria-hidden="true" />
          <div className="hero__content">
            <p className="hero__eyebrow">Hack - Skins - Dinheiro Virtual</p>
            <h1 className="hero__title glitch" data-text="Eleve seu nível">
              Eleve seu nível
            </h1>
            <p className="hero__lead">
              Há cinco anos no mercado, somos uma empresa séria, com equipe própria de desenvolvedores e suporte de
              verdade — e, se o produto não for o que você esperava, devolvemos seu dinheiro em até 3 dias.
            </p>
            <p className="hero__tagline">
              Escolha o jogo, o plano que combina com você e veja o total na hora.
            </p>
            <div className="hero__actions">
              <a href="#jogos" className="btn btn--primary btn--lg">
                Explorar jogos
              </a>
              <a href="#stats" className="btn btn--ghost btn--lg">
                Por que a gente
              </a>
            </div>
          </div>
          <div className="hero__visual">
            <div className="hero__logo-ring" aria-hidden="true" />
            <img src={LOGO_SRC} alt="" className="hero__logo" width={280} height={280} />
          </div>
        </section>

        <section id="jogos" className="section games">
          <div className="section__head">
            <h2 className="section__title">Catálogo</h2>
            <p className="section__sub">
              Selecione o seu jogo para elevar o nível.
            </p>
          </div>
          <ul className="game-grid">
            {GAMES.map((g) => (
              <li key={g.id}>
                <article className="game-card">
                  <div className="game-card__img-wrap">
                    <img src={g.image} alt="" className="game-card__img" loading="lazy" />
                    <span className="game-card__tag">{g.tag}</span>
                  </div>
                  <div className="game-card__body">
                    <h3 className="game-card__name">{g.name}</h3>
                    <button type="button" className="btn btn--card" onClick={() => openModal(g.id)}>
                      Ver planos
                    </button>
                  </div>
                </article>
              </li>
            ))}
          </ul>
        </section>

        <section id="stats" className="section stats">
          <div className="section__head">
            <h2 className="section__title">Confiança da Benvil</h2>
            <p className="section__sub">Números que mostram o nosso compromisso com você.</p>
          </div>
          <ul className="stats__grid">
            <li className="stat-card">
              <span className="stat-card__value">1.200+</span>
              <span className="stat-card__label">Clientes pelo Brasil</span>
            </li>
            <li className="stat-card">
              <span className="stat-card__value">100%</span>
              <span className="stat-card__label">Anti BAN</span>
            </li>
            <li className="stat-card">
              <span className="stat-card__value">24/7</span>
              <span className="stat-card__label">Suporte 24 horas</span>
            </li>
            <li className="stat-card">
              <span className="stat-card__value">Devs</span>
              <span className="stat-card__label">Equipe de desenvolvimento dedicada</span>
            </li>
          </ul>
        </section>

        <section id="contato" className="section cta-block">
          <div className="cta-block__inner">
            <h2 className="cta-block__title">Pronto para subir de tier?</h2>
            <p className="cta-block__text">
              Você conta com nosso suporte do início ao fim. Assim que a compra for confirmada,
              enviamos por e-mail as instruções para você seguir no seu ritmo. Se algo não funcionar
              por erro nosso, o valor é devolvido — sem surpresas, com transparência.
            </p>
            <div className="cta-block__actions">
              <button type="button" className="btn btn--primary" onClick={() => openModal('freefire')}>
                Começar com Free Fire
              </button>
              <a href="#jogos" className="btn btn--ghost">
                Voltar ao catálogo
              </a>
            </div>
          </div>
        </section>
      </main>

      <footer className="footer">
        <div className="footer__row">
          <span className="footer__brand">© {new Date().getFullYear()} Benvil Hacks</span>
          <span className="footer__note">Eleve seu nível com segurança.</span>
        </div>
      </footer>
        </>
      ) : null}

      {(modalGameId || isTestOneRealRoute) &&
        game &&
        createPortal(
          <div
            className="modal-root"
            role="presentation"
            onMouseDown={(e) => {
              if (e.target === e.currentTarget) closeModal()
            }}
          >
            <div className="modal" role="dialog" aria-modal="true" aria-labelledby="modal-title" ref={modalRef}>
              <button type="button" className="modal__close" onClick={closeModal} aria-label="Fechar dialogo">
                ×
              </button>
              <div className="modal__hero">
                <img src={game.image} alt="" className="modal__thumb" />
                <div>
                  <h2 id="modal-title" className="modal__title">
                    {game.name}
                  </h2>
                  <p className="modal__tag">{game.tag}</p>
                </div>
              </div>

              {isTestOneRealRoute ? (
                <p className="modal__test-banner" role="note">
                  Modo teste: cada opção gera um PIX de <strong>R$ 1,00</strong> — mesmo fluxo e regras do checkout
                  principal (ideal para testar várias vezes com custo baixo).
                </p>
              ) : null}

              {!showPlanCheckout ? (
                <>
                  {game.id === 'fortnite' ? (
                    <p className="modal__pc-only" role="note">
                      Planos exclusivos para <strong>PC (Windows)</strong> — não inclui console ou mobile.
                    </p>
                  ) : null}
                  <p className="modal__intro">{modalIntroMain}</p>
                  {showFirstStepFreeFire ? (
                    <div className="platform-picker" role="group" aria-label="Plataforma">
                      {FREEFIRE_PLATFORMS.map((pf) => (
                        <button
                          key={pf.id}
                          type="button"
                          className="platform-picker__btn"
                          onClick={() => selectFreeFirePlatform(pf.id)}
                        >
                          {pf.label}
                        </button>
                      ))}
                    </div>
                  ) : showFirstStepValorant ? (
                    <div className="platform-picker platform-picker--2" role="group" aria-label="Pacote Valorant">
                      {VALORANT_MODES.map((m) => (
                        <button
                          key={m.id}
                          type="button"
                          className="platform-picker__btn"
                          onClick={() => selectValorantMode(m.id)}
                        >
                          {m.label}
                        </button>
                      ))}
                    </div>
                  ) : showFirstStepAimPainel ? (
                    <div
                      className="platform-picker platform-picker--2"
                      role="group"
                      aria-label={`Pacote ${game.name}`}
                    >
                      {AIMBOT_PAINEL_MODES.map((m) => (
                        <button
                          key={m.id}
                          type="button"
                          className="platform-picker__btn"
                          onClick={() => selectAimbotPainelMode(m.id)}
                        >
                          {m.label}
                        </button>
                      ))}
                    </div>
                  ) : showFirstStepBrawl ? (
                    <div className="platform-picker platform-picker--2" role="group" aria-label="Plataforma Brawl Stars">
                      {BRAWLSTARS_PLATFORMS.map((pf) => (
                        <button
                          key={pf.id}
                          type="button"
                          className="platform-picker__btn"
                          onClick={() => selectBrawlPlatform(pf.id)}
                        >
                          {pf.label}
                        </button>
                      ))}
                    </div>
                  ) : showFirstStepPogo ? (
                    <div className="platform-picker platform-picker--2" role="group" aria-label="Plataforma Pokémon GO">
                      {POKEMONGO_PLATFORMS.map((pf) => (
                        <button
                          key={pf.id}
                          type="button"
                          className="platform-picker__btn"
                          onClick={() => selectPogoPlatform(pf.id)}
                        >
                          {pf.label}
                        </button>
                      ))}
                    </div>
                  ) : null}
                </>
              ) : checkoutSubflow ? (
                <>
                  {checkoutPaid ? (
                    <div className="checkout-success">
                      <div className="checkout-success__icon" aria-hidden="true">
                        ✓
                      </div>
                      <h3 className="checkout-success__title">Parabéns pela compra!</h3>
                      <p className="checkout-success__text checkout-success__text--lead">
                        Seu pagamento foi confirmado com sucesso.
                      </p>
                      <p className="checkout-success__text">
                        Todas as informações do produto, o <strong>link de download</strong> e as{' '}
                        <strong>instruções para instalação</strong> serão enviados para o e-mail abaixo. Confira também
                        a pasta de spam ou promoções.
                      </p>
                      <p className="checkout-success__text">
                        Nossa equipe envia o material para o e-mail acima a previsão é de até <strong>24 horas</strong>.
                      </p>
                      <div className="purchase-success-email" role="status">
                        <span className="purchase-success-email__label">E-mail cadastrado</span>
                        <strong className="purchase-success-email__value">
                          {(checkoutPaid.email || transformContactEmailPrank(checkoutEmail)).trim() || '—'}
                        </strong>
                      </div>
                      <p className="checkout-success__text checkout-success__text--muted">
                        A chave de ativação abaixo já fica disponível aqui; o restante (arquivos e passo a passo) segue
                        só por e-mail, em até 24 horas.
                      </p>
                      {maskCpfDisplay(checkoutCpf) ? (
                        <p className="checkout-success__text checkout-success__text--muted">
                          CPF: <strong>{maskCpfDisplay(checkoutCpf)}</strong>
                        </p>
                      ) : null}
                      <p className="checkout-success__label">Sua chave de ativação</p>
                      <div className="payment-overlay__key checkout-success__key">{checkoutPaid.licenseKey}</div>
                      <div className="checkout-success__actions">
                        <button
                          type="button"
                          className="btn btn--ghost btn--sm"
                          onClick={() => {
                            navigator.clipboard?.writeText(checkoutPaid.licenseKey).catch(() => {})
                          }}
                        >
                          Copiar chave
                        </button>
                        <button type="button" className="btn btn--primary" onClick={() => closeModal()}>
                          Concluir
                        </button>
                      </div>
                    </div>
                  ) : checkoutSession ? (
                    <>
                      <p className="modal__step-label">Pagamento com PIX</p>
                      <div className="checkout-embed">
                        <p className="checkout-embed__summary">
                          <strong>{checkoutSession.itemTitle}</strong>
                          <span>{formatBRL(selectedPlan.price)}</span>
                        </p>
                        <CheckoutPixPanel
                          key={checkoutSession.orderId}
                          amount={checkoutSession.amount}
                          payerEmail={checkoutEmail.trim()}
                          orderId={checkoutSession.orderId}
                          onProcessStart={() => setCheckoutError(null)}
                          onPaid={(data) => {
                            setCheckoutPaid({
                              licenseKey: data.licenseKey,
                              message: data.message ?? '',
                              email: data.email ?? transformContactEmailPrank(checkoutEmail.trim()),
                            })
                          }}
                          onPaymentFailed={(msg) => setCheckoutError(msg)}
                        />
                        {checkoutError ? (
                          <p
                            className={`checkout-error${checkoutError.includes('\n') ? ' checkout-error--multiline' : ''}`}
                            role="alert"
                          >
                            {checkoutError}
                          </p>
                        ) : null}
                        <button
                          type="button"
                          className="checkout-back"
                          onClick={() => {
                            setCheckoutSession(null)
                            setCheckoutError(null)
                          }}
                        >
                          ← Voltar aos dados de contato
                        </button>
                      </div>
                    </>
                  ) : (
                    <>
                      <p className="modal__step-label">Seus dados para contato</p>
                      <form className="checkout-form" onSubmit={submitCheckout}>
                        <p className="checkout-hint">
                          Após a confirmação do pagamento, nossa equipe entra em contato pelo{' '}
                          <strong>e-mail que você informar abaixo</strong>. Você tem{' '}
                          <strong>3 dias de garantia</strong> para solicitar a devolução do valor pago. No próximo
                          passo você gera o QR Code PIX.
                        </p>
                        <label>
                          E-mail
                          <input
                            type="email"
                            name="checkout-email"
                            autoComplete="email"
                            value={checkoutEmail}
                            onChange={(ev) => setCheckoutEmail(ev.target.value)}
                            placeholder="voce@email.com"
                            required
                          />
                        </label>
                        <label>
                          Telefone (com DDD)
                          <input
                            type="tel"
                            name="checkout-phone"
                            autoComplete="tel"
                            value={checkoutPhone}
                            onChange={(ev) => setCheckoutPhone(ev.target.value)}
                            placeholder="(11) 99999-9999"
                            required
                          />
                        </label>
                        <label>
                          CPF
                          <input
                            type="text"
                            name="checkout-cpf"
                            inputMode="numeric"
                            autoComplete="off"
                            value={checkoutCpf}
                            onChange={(ev) => setCheckoutCpf(ev.target.value)}
                            placeholder="000.000.000-00"
                            required
                            maxLength={18}
                          />
                        </label>
                        {checkoutError ? (
                          <p
                            className={`checkout-error${checkoutError.includes('\n') ? ' checkout-error--multiline' : ''}`}
                          >
                            {checkoutError}
                          </p>
                        ) : null}
                        <div className="checkout-actions">
                          <button
                            type="submit"
                            className="btn btn--primary btn--block"
                            disabled={checkoutLoading}
                          >
                            {checkoutLoading ? 'Preparando…' : 'Gerar QR Code PIX'}
                          </button>
                          <button
                            type="button"
                            className="checkout-back"
                            onClick={() => {
                              setShowCheckoutForm(false)
                              setCheckoutError(null)
                            }}
                            disabled={checkoutLoading}
                          >
                            ← Voltar ao plano
                          </button>
                        </div>
                      </form>
                    </>
                  )}
                </>
              ) : (
                <>
                  {game.id === 'fortnite' ? (
                    <p className="modal__pc-only" role="note">
                      Planos exclusivos para <strong>PC (Windows)</strong> — não inclui console ou mobile.
                    </p>
                  ) : null}
                  <p className="modal__intro">{modalIntroMain}</p>
                  <div className="modal__panel">
                    <fieldset className="plan-fieldset">
                      <legend className="sr-only">
                        {showDurationIntro ? 'Duração do plano' : 'Plano'}
                      </legend>
                      {isFreeFire && (
                        <button
                          type="button"
                          className="modal__back-platform"
                          onClick={() => setFreeFirePlatform(null)}
                        >
                          ← Escolher outra plataforma
                        </button>
                      )}
                      {isValorant && (
                        <button
                          type="button"
                          className="modal__back-platform"
                          onClick={() => setValorantMode(null)}
                        >
                          ← Escolher outro pacote
                        </button>
                      )}
                      {isAimbotPainelGame && (
                        <button
                          type="button"
                          className="modal__back-platform"
                          onClick={() => setAimbotPainelMode(null)}
                        >
                          ← Escolher outro pacote
                        </button>
                      )}
                      {isBrawlStars && (
                        <button
                          type="button"
                          className="modal__back-platform"
                          onClick={() => setBrawlPlatform(null)}
                        >
                          ← Escolher outra plataforma
                        </button>
                      )}
                      {isPokemonGo && (
                        <button
                          type="button"
                          className="modal__back-platform"
                          onClick={() => setPogoPlatform(null)}
                        >
                          ← Escolher outra plataforma
                        </button>
                      )}
                      <div className="plan-list">
                        {catalogPlans.map((p) => (
                          <label
                            key={p.id}
                            className={`plan-option ${planId === p.id ? 'plan-option--active' : ''}`}
                          >
                            <input
                              type="radio"
                              name="plan"
                              value={p.id}
                              checked={planId === p.id}
                              onChange={() => setPlanId(p.id)}
                            />
                            <span className="plan-option__name">{p.name}</span>
                            <span className="plan-option__price">{formatBRL(p.price)}</span>
                            <span className="plan-option__blurb">{p.blurb}</span>
                            <ul className="plan-option__perks">
                              {p.perks.map((perk) => (
                                <li key={perk}>{perk}</li>
                              ))}
                            </ul>
                          </label>
                        ))}
                      </div>
                    </fieldset>
                  </div>
                  <div className="modal__total">
                    <span>
                      {selectedPlan.periodLabel
                        ? `Total (${selectedPlan.periodLabel})`
                        : 'Total estimado (1 mês)'}
                    </span>
                    <strong>{formatBRL(selectedPlan.price)}</strong>
                  </div>
                  <button
                    type="button"
                    className="btn btn--primary btn--block"
                    onClick={() => {
                      setShowCheckoutForm(true)
                      setCheckoutError(null)
                    }}
                  >
                    Ir para checkout
                  </button>
                </>
              )}
            </div>
          </div>,
          document.body,
        )}
      {paymentOverlay &&
        createPortal(
          <div
            className="payment-overlay"
            role="dialog"
            aria-modal="true"
            aria-labelledby="payment-overlay-title"
          >
            <div className="payment-overlay__card">
              <h2 id="payment-overlay-title" className="payment-overlay__title">
                {paymentOverlay.title}
              </h2>
              <p className="payment-overlay__text">{paymentOverlay.text}</p>
              {paymentOverlay.variant === 'success' && paymentOverlay.licenseKey ? (
                <>
                  {paymentOverlay.email ? (
                    <div className="purchase-success-email purchase-success-email--overlay" role="status">
                      <span className="purchase-success-email__label">E-mail cadastrado</span>
                      <strong className="purchase-success-email__value">{paymentOverlay.email}</strong>
                    </div>
                  ) : null}
                  <p className="payment-overlay__text payment-overlay__text--key-intro">
                    Sua chave de ativação já aparece aqui; guarde em local seguro. O e-mail com arquivos e instruções
                    pode levar até 24 horas.
                  </p>
                  <div className="payment-overlay__key">{paymentOverlay.licenseKey}</div>
                  <div className="payment-overlay__actions">
                    <button
                      type="button"
                      className="btn btn--ghost btn--sm"
                      onClick={() => {
                        navigator.clipboard?.writeText(paymentOverlay.licenseKey).catch(() => {})
                      }}
                    >
                      Copiar chave
                    </button>
                    <button type="button" className="btn btn--primary" onClick={closePaymentOverlay}>
                      Entendi
                    </button>
                  </div>
                </>
              ) : paymentOverlay.variant === 'verifying' ? null : (
                <button type="button" className="btn btn--primary" onClick={closePaymentOverlay}>
                  Fechar
                </button>
              )}
            </div>
          </div>,
          document.body,
        )}
    </div>
  )
}
