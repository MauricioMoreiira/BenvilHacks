import { useCallback, useEffect, useRef, useState } from 'react'
import { apiPath } from './api'

function buildPixErrText(data) {
  const lines = []
  if (typeof data.message === 'string' && data.message.trim()) lines.push(data.message.trim())
  if (data.mpHttpStatus != null) lines.push(`HTTP (Mercado Pago): ${data.mpHttpStatus}`)
  const mpBody = data.mpResponseBody || data.detail
  if (typeof mpBody === 'string' && mpBody.trim()) lines.push(`Resposta do Mercado Pago:\n${mpBody.trim()}`)
  if (typeof data.mpRequestJson === 'string' && data.mpRequestJson.trim())
    lines.push(`JSON enviado ao MP (POST /v1/payments):\n${data.mpRequestJson.trim()}`)
  if (Array.isArray(data.mpAttempts) && data.mpAttempts.length > 0)
    lines.push(`Todas as tentativas (request/response):\n${JSON.stringify(data.mpAttempts, null, 2)}`)
  if (Array.isArray(data.mpEnvironmentHints) && data.mpEnvironmentHints.length > 0)
    lines.push(`Ambiente / credenciais (Mercado Pago):\n${data.mpEnvironmentHints.join('\n')}`)
  return lines.length > 0 ? lines.join('\n\n') : 'Não foi possível gerar o PIX.'
}

async function requestCreatePix(orderId, amount) {
  const res = await fetch(apiPath('/api/checkout/create-pix'), {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
    body: JSON.stringify({
      orderId,
      transaction_amount: amount,
    }),
  })
  const data = await res.json().catch(() => ({}))
  return { res, data }
}

/**
 * PIX: gera o QR automaticamente; mensagem sobre download, instalação e garantia fica acima do QR.
 */
export function CheckoutPixPanel({
  orderId,
  amount,
  payerEmail,
  onPaid,
  onPaymentFailed: onPixPaymentFailed,
  onProcessStart,
}) {
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)
  const [pixInfo, setPixInfo] = useState(null)
  const attemptsRef = useRef(0)
  const intervalRef = useRef(null)
  const onPaidRef = useRef(onPaid)
  const onFailRef = useRef(onPixPaymentFailed)
  const onProcessStartRef = useRef(onProcessStart)
  const emailFallback = (payerEmail ?? '').trim()

  useEffect(() => {
    onPaidRef.current = onPaid
  }, [onPaid])

  useEffect(() => {
    onFailRef.current = onPixPaymentFailed
  }, [onPixPaymentFailed])

  useEffect(() => {
    onProcessStartRef.current = onProcessStart
  }, [onProcessStart])

  const applyCreatePixResult = useCallback(
    (res, data) => {
      if (!res.ok) {
        setError(buildPixErrText(data))
        return
      }
      if (!data.ok) {
        setError(buildPixErrText(data))
        return
      }
      if (data.licenseKey) {
        onPaidRef.current?.({
          licenseKey: data.licenseKey,
          message: data.message ?? '',
          email: data.email ?? emailFallback,
        })
        return
      }
      if (data.awaitingPixTransfer && data.paymentId) {
        attemptsRef.current = 0
        setPixInfo(data)
        return
      }
      setError('Resposta inesperada do servidor. Tente de novo.')
    },
    [emailFallback],
  )

  const runCreatePix = useCallback(async () => {
    setError(null)
    onProcessStartRef.current?.()
    setLoading(true)
    try {
      const { res, data } = await requestCreatePix(orderId, amount)
      applyCreatePixResult(res, data)
    } catch {
      setError('Falha de rede. Tente de novo.')
    } finally {
      setLoading(false)
    }
  }, [orderId, amount, applyCreatePixResult])

  useEffect(() => {
    let cancelled = false
    const go = async () => {
      setPixInfo(null)
      setError(null)
      onProcessStartRef.current?.()
      setLoading(true)
      try {
        const { res, data } = await requestCreatePix(orderId, amount)
        if (cancelled) return
        applyCreatePixResult(res, data)
      } catch {
        if (!cancelled) setError('Falha de rede. Tente de novo.')
      } finally {
        if (!cancelled) setLoading(false)
      }
    }
    void go()
    return () => {
      cancelled = true
    }
  }, [orderId, amount, applyCreatePixResult])

  useEffect(() => {
    if (!pixInfo?.paymentId) return undefined

    const tick = async () => {
      attemptsRef.current += 1
      if (attemptsRef.current > 150) {
        if (intervalRef.current) clearInterval(intervalRef.current)
        const msg =
          'Ainda não detectamos o PIX. Se você já pagou, guarde o comprovante e fale com o suporte ou atualize a página.'
        setError(msg)
        onFailRef.current?.(msg)
        return
      }
      try {
        const res = await fetch(apiPath('/api/checkout/verify'), {
          method: 'POST',
          headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
          body: JSON.stringify({
            orderId,
            paymentId: String(pixInfo.paymentId),
          }),
        })
        const data = await res.json().catch(() => ({}))
        if (data.ok && data.licenseKey) {
          if (intervalRef.current) clearInterval(intervalRef.current)
          onPaidRef.current?.({
            licenseKey: data.licenseKey,
            message: data.message ?? '',
            email: data.email ?? emailFallback,
          })
        }
      } catch {
        /* próxima tentativa */
      }
    }

    intervalRef.current = setInterval(tick, 4000)
    tick()
    return () => {
      if (intervalRef.current) clearInterval(intervalRef.current)
      intervalRef.current = null
    }
  }, [pixInfo, orderId, emailFallback])

  const src = pixInfo?.qrCodeBase64 ? `data:image/png;base64,${pixInfo.qrCodeBase64}` : null

  return (
    <div className="checkout-pix-flow">
      <p className="checkout-pix-flow__intro">
        Após o pagamento ser confirmado, você será orientado sobre o <strong>download</strong> e a{' '}
        <strong>instalação</strong> do produto. Você tem <strong>3 dias</strong> para pedir reembolso, conforme nossa
        garantia.
      </p>

      {loading && !pixInfo ? (
        <p className="checkout-pix-flow__loading">Gerando QR Code PIX…</p>
      ) : null}

      {error && !pixInfo && !loading ? (
        <>
          <p className={`checkout-error${error.includes('\n') ? ' checkout-error--multiline' : ''}`}>{error}</p>
          <button
            type="button"
            className="btn btn--primary btn--block checkout-pix-flow__retry"
            onClick={() => void runCreatePix()}
          >
            Tentar gerar o PIX de novo
          </button>
        </>
      ) : null}

      {pixInfo ? (
        <div className="checkout-pix-flow__payment">
          <p className="checkout-hint checkout-hint--compact checkout-pix-flow__pay-hint">
            {pixInfo.message || 'Escaneie o QR Code no app do seu banco ou use o Pix copia e cola abaixo.'}
          </p>
          {src ? (
            <div className="checkout-pix-wait__qr">
              <img src={src} alt="QR Code PIX" width={220} height={220} />
            </div>
          ) : null}
          {pixInfo.qrCode ? (
            <label className="checkout-pix-wait__copy">
              <span>Pix copia e cola</span>
              <div className="checkout-pix-wait__copy-row">
                <input type="text" readOnly value={pixInfo.qrCode} className="checkout-pix-wait__input" />
                <button
                  type="button"
                  className="btn btn--ghost btn--sm"
                  onClick={() => {
                    navigator.clipboard?.writeText(pixInfo.qrCode).catch(() => {})
                  }}
                >
                  Copiar
                </button>
              </div>
            </label>
          ) : null}
          {pixInfo.ticketUrl ? (
            <p className="checkout-pix-wait__ticket">
              <a href={pixInfo.ticketUrl} target="_blank" rel="noreferrer">
                Abrir página do pagamento PIX
              </a>
            </p>
          ) : null}
          <p className="checkout-pix-wait__status">Aguardando confirmação do pagamento…</p>
          {error ? <p className="checkout-error">{error}</p> : null}
          <button
            type="button"
            className="checkout-back"
            onClick={() => {
              setPixInfo(null)
              setError(null)
              attemptsRef.current = 0
              void runCreatePix()
            }}
          >
            ← Gerar novo código PIX
          </button>
        </div>
      ) : null}
    </div>
  )
}
