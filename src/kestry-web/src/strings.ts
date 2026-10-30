// All user-visible text lives here so a second language can be added later.
export const strings = {
  appName: 'Kestry',
  status: {
    downloading: 'Descargando',
    queued: 'En cola',
    paused: 'Pausada',
    completed: 'Completada',
    failed: 'Error',
  },
  progressLabel: (name: string) => `Progreso de ${name}`,
  fetchingMetadata: 'Obteniendo metadatos…',
  waiting: 'En espera',
  downloadFailed: 'La descarga ha fallado',
  of: 'de',
  perSecond: '/s',
  etaLeft: {
    seconds: (n: number) => `quedan ${n} s`,
    minutes: (n: number) => `quedan ${n} min`,
    hoursMinutes: (h: number, m: number) => `quedan ${h} h ${m} min`,
  },
  peers: (connections: number, seeders: number) => `${connections} conexiones · ${seeders} seeds`,
  actions: {
    pause: 'Pausar',
    resume: 'Reanudar',
    cancel: 'Cancelar',
    retry: 'Reintentar',
    remove: 'Eliminar',
    copyPath: 'Copiar ruta',
    more: 'Más acciones',
    close: 'Cerrar',
    dismiss: 'Descartar',
  },
  fileDrop: {
    prompt: 'Suelta archivos .torrent aquí o haz clic para elegirlos',
    selected: (n: number) => (n === 1 ? '1 archivo seleccionado' : `${n} archivos seleccionados`),
  },
} as const;
