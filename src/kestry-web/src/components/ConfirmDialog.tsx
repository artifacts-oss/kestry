import { useState } from 'react';
import { Button } from './ui/Button';
import { Checkbox } from './ui/fields';
import { Dialog } from './ui/overlays';
import { useTranslations } from '../i18n';

export type Confirmation = { kind: 'cancel' | 'remove'; gid: string; name: string };

/** Confirms cancelling (always deletes partial files) or removing a finished download (optionally deleting files). */
export function ConfirmDialog({ confirmation, onConfirm, onClose }: {
  confirmation: Confirmation | null;
  onConfirm: (c: Confirmation, deleteFiles: boolean) => void;
  onClose: () => void;
}) {
  const [deleteFiles, setDeleteFiles] = useState(false);
  const t = useTranslations('Confirm');
  const cancel = confirmation?.kind === 'cancel';
  return (
    <Dialog
      open={confirmation !== null}
      title={cancel ? t('cancelTitle') : t('removeTitle')}
      onClose={onClose}
      footer={
        <>
          <Button variant="quiet" onClick={onClose}>{t('cancelNo')}</Button>
          <Button
            variant="danger"
            onClick={() => {
              onConfirm(confirmation!, cancel || deleteFiles);
              setDeleteFiles(false);
            }}
          >
            {cancel ? t('cancelYes') : t('removeYes')}
          </Button>
        </>
      }
    >
      {confirmation && (
        <>
          <p className="m-0 text-body-sm text-ink">{cancel ? t('cancelBody', { name: confirmation.name }) : t('removeBody', { name: confirmation.name })}</p>
          {!cancel && <Checkbox label={t('removeFiles')} checked={deleteFiles} onChange={(e) => setDeleteFiles(e.target.checked)} />}
        </>
      )}
    </Dialog>
  );
}
