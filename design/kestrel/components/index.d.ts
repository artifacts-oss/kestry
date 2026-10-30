import type * as React from 'react';
export type IconName = 'download'|'pause'|'play'|'x'|'plus'|'check'|'retry'|'folder'|'search'|'alert'|'info'|'file'|'video'|'archive'|'music'|'image'|'trash'|'link'|'clock'|'inbox'|'sliders'|'chevronDown'|'gauge';
export type DownloadStatus = 'downloading'|'queued'|'paused'|'completed'|'failed';
export interface IconProps { name: IconName; size?: number; label?: string; className?: string }
export declare function Icon(props: IconProps): React.ReactElement;
export interface ButtonProps extends React.ButtonHTMLAttributes<HTMLButtonElement> { variant?: 'primary'|'accent'|'secondary'|'quiet'|'danger'; size?: 'sm'|'md'; icon?: IconName }
export declare function Button(props: ButtonProps): React.ReactElement;
export interface IconButtonProps extends React.ButtonHTMLAttributes<HTMLButtonElement> { icon: IconName; label: string; variant?: 'quiet'|'secondary'; size?: 'sm'|'md' }
export declare function IconButton(props: IconButtonProps): React.ReactElement;
export interface TextFieldProps extends React.InputHTMLAttributes<HTMLInputElement> { label?: string; hint?: string; error?: string; icon?: IconName; mono?: boolean }
export declare function TextField(props: TextFieldProps): React.ReactElement;
export interface SelectProps extends React.SelectHTMLAttributes<HTMLSelectElement> { label?: string; hint?: string; options: Array<string | { value: string; label: string }> }
export declare function Select(props: SelectProps): React.ReactElement;
export interface CheckboxProps extends React.InputHTMLAttributes<HTMLInputElement> { label: React.ReactNode }
export declare function Checkbox(props: CheckboxProps): React.ReactElement;
export interface SwitchProps { checked: boolean; onChange?: (checked: boolean) => void; label?: string; disabled?: boolean; className?: string }
export declare function Switch(props: SwitchProps): React.ReactElement;
export interface StatusBadgeProps { status: DownloadStatus; children?: React.ReactNode }
export declare function StatusBadge(props: StatusBadgeProps): React.ReactElement;
/** value 0–100; omit for indeterminate. */
export interface ProgressBarProps { value?: number; status?: DownloadStatus; label?: string; className?: string }
export declare function ProgressBar(props: ProgressBarProps): React.ReactElement;
/** Sizes in bytes, speed in bytes/s, eta in seconds. */
export interface DownloadItemProps { name: string; status: DownloadStatus; fileType?: 'file'|'video'|'archive'|'music'|'image'; size?: number; downloaded?: number; speed?: number; eta?: number; error?: string; onPause?(): void; onResume?(): void; onCancel?(): void; onRetry?(): void; onOpen?(): void; onRemove?(): void; className?: string }
export declare function DownloadItem(props: DownloadItemProps): React.ReactElement;
export interface NavItemProps extends React.ButtonHTMLAttributes<HTMLButtonElement> { label: string; icon?: IconName; count?: number; active?: boolean }
export declare function NavItem(props: NavItemProps): React.ReactElement;
export interface ToolbarProps { label?: string; children?: React.ReactNode; className?: string }
export declare const Toolbar: ((props: ToolbarProps) => React.ReactElement) & { Spacer: () => React.ReactElement };
export interface SpeedStatProps { label: string; value: React.ReactNode; unit?: string; icon?: IconName; className?: string }
export declare function SpeedStat(props: SpeedStatProps): React.ReactElement;
export interface DialogProps { open: boolean; title: string; onClose?(): void; footer?: React.ReactNode; children?: React.ReactNode }
export declare function Dialog(props: DialogProps): React.ReactElement | null;
export interface ToastProps { tone?: 'info'|'success'|'error'; children: React.ReactNode; actionLabel?: string; onAction?(): void; onClose?(): void; className?: string }
export declare function Toast(props: ToastProps): React.ReactElement;
export interface EmptyStateProps { title: string; description?: string; icon?: IconName; action?: React.ReactNode; className?: string }
export declare function EmptyState(props: EmptyStateProps): React.ReactElement;
export declare function formatBytes(bytes: number): string;
declare global { interface Window { Kestrel: { Icon: typeof Icon; Button: typeof Button; IconButton: typeof IconButton; TextField: typeof TextField; Select: typeof Select; Checkbox: typeof Checkbox; Switch: typeof Switch; StatusBadge: typeof StatusBadge; ProgressBar: typeof ProgressBar; DownloadItem: typeof DownloadItem; NavItem: typeof NavItem; Toolbar: typeof Toolbar; SpeedStat: typeof SpeedStat; Dialog: typeof Dialog; Toast: typeof Toast; EmptyState: typeof EmptyState; formatBytes: typeof formatBytes; iconNames: IconName[] } } }
