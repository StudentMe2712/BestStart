/**
 * Лёгкий клиентский кэш в памяти (живёт в течение SPA-сессии, сбрасывается при
 * полной перезагрузке). Цель — мгновенный рендер при возврате на страницу:
 * показываем закэшированные данные сразу, а свежие подгружаем в фоне.
 *
 * Это не замена серверному кэшу (Redis) — для локального single-user приложения
 * против удалённого Neon именно «мгновенный возврат на вкладку» даёт основной
 * выигрыш в ощущаемой скорости переходов.
 */
const store = new Map<string, unknown>()

export function getCache<T>(key: string): T | undefined {
  return store.get(key) as T | undefined
}

export function setCache<T>(key: string, value: T): void {
  store.set(key, value)
}
