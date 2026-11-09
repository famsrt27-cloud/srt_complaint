// ─── Thai Date Pickers (Flatpickr) ───────────────────────────────────────────
(function initThaiDatePickers() {
    var MONTHS_LONG  = ['มกราคม','กุมภาพันธ์','มีนาคม','เมษายน','พฤษภาคม','มิถุนายน',
                        'กรกฎาคม','สิงหาคม','กันยายน','ตุลาคม','พฤศจิกายน','ธันวาคม'];
    var MONTHS_SHORT = ['ม.ค.','ก.พ.','มี.ค.','เม.ย.','พ.ค.','มิ.ย.',
                        'ก.ค.','ส.ค.','ก.ย.','ต.ค.','พ.ย.','ธ.ค.'];
    var DAYS_SHORT   = ['อา.','จ.','อ.','พ.','พฤ.','ศ.','ส.'];
    var DAYS_LONG    = ['อาทิตย์','จันทร์','อังคาร','พุธ','พฤหัสบดี','ศุกร์','เสาร์'];

    function toBE(date) {
        return date.getDate() + ' ' + MONTHS_LONG[date.getMonth()] + ' ' + (date.getFullYear() + 543);
    }

    document.addEventListener('DOMContentLoaded', function () {
        if (typeof flatpickr === 'undefined') return;

        document.querySelectorAll('[data-datepicker]').forEach(function (el) {
            var originalClass = el.getAttribute('class') || '';
            var maxDate = el.getAttribute('max') || undefined;
            var minDate = el.getAttribute('min') || undefined;

            function showBE(fp) {
                if (fp.selectedDates.length > 0 && fp.altInput) {
                    fp.altInput.value = toBE(fp.selectedDates[0]);
                }
            }

            flatpickr(el, {
                locale: {
                    months:      { longhand: MONTHS_LONG, shorthand: MONTHS_SHORT },
                    weekdays:    { longhand: DAYS_LONG,   shorthand: DAYS_SHORT },
                    firstDayOfWeek: 0,
                    rangeSeparator: ' ถึง '
                },
                dateFormat:    'Y-m-d',
                altInput:      true,
                altInputClass: originalClass,
                altFormat:     'j F Y',   // fallback; overridden by showBE
                maxDate:       maxDate,
                minDate:       minDate,
                onReady:       function (_, __, fp) {
                    showBE(fp);
                    if (fp.altInput) fp.altInput.placeholder = 'วัน เดือน ปี (พ.ศ.)';
                },
                onChange:      function (_, __, fp) { showBE(fp); }
            });
        });
    });
})();
// ─────────────────────────────────────────────────────────────────────────────

// ─── Input Guards ────────────────────────────────────────────────────────────
// data-block="thai"        → ห้ามพิมพ์อักขระไทย (U+0E00–U+0E7F)
// data-block="digits-only" → รับเฉพาะตัวเลข 0-9
(function initInputGuards() {
    var THAI = /[฀-๿]/;

    function applyThaiBlock(el) {
        el.addEventListener('keydown', function (e) {
            if (e.key.length === 1 && THAI.test(e.key)) e.preventDefault();
        });
        el.addEventListener('input', function () {
            var clean = this.value.replace(/[฀-๿]/g, '');
            if (clean !== this.value) {
                var pos = this.selectionStart - (this.value.length - clean.length);
                this.value = clean;
                this.setSelectionRange(pos, pos);
            }
        });
    }

    function applyDigitsOnly(el) {
        var NAV = new Set(['Backspace','Delete','Tab','Enter',
                           'ArrowLeft','ArrowRight','ArrowUp','ArrowDown','Home','End']);
        el.addEventListener('keydown', function (e) {
            if (NAV.has(e.key) || e.ctrlKey || e.metaKey) return;
            if (!/^\d$/.test(e.key)) e.preventDefault();
        });
        el.addEventListener('input', function () {
            var clean = this.value.replace(/\D/g, '');
            if (clean !== this.value) this.value = clean;
        });
    }

    document.addEventListener('DOMContentLoaded', function () {
        document.querySelectorAll('[data-block="thai"]').forEach(applyThaiBlock);
        document.querySelectorAll('[data-block="digits-only"]').forEach(applyDigitsOnly);
    });
})();
// ─────────────────────────────────────────────────────────────────────────────

// ─── Shared SweetAlert2 dialogs ──────────────────────────────────────────────
(function initSharedDialogs() {
    const approvedForms = new WeakSet();
    const getSwal = () => window.Swal;
    const baseConfirmClass = 'inline-flex min-h-11 items-center justify-center rounded-lg bg-srt-navy px-5 py-2.5 text-sm font-semibold text-white hover:bg-srt-navy-dark focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-srt-navy focus-visible:ring-offset-2';
    const dangerConfirmClass = 'inline-flex min-h-11 items-center justify-center rounded-lg bg-red-700 px-5 py-2.5 text-sm font-semibold text-white hover:bg-red-800 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-red-700 focus-visible:ring-offset-2';
    const cancelClass = 'inline-flex min-h-11 items-center justify-center rounded-lg border border-slate-300 bg-white px-5 py-2.5 text-sm font-semibold text-slate-700 hover:bg-slate-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-slate-400 focus-visible:ring-offset-2';

    window.SrtDialog = {
        async confirm(message, options = {}) {
            const swal = getSwal();
            if (!swal) return false;
            const result = await swal.fire({
                title: options.title || 'ยืนยันการดำเนินการ',
                text: message,
                icon: options.icon || 'question',
                showCancelButton: true,
                confirmButtonText: options.confirmText || 'ยืนยัน',
                cancelButtonText: options.cancelText || 'ยกเลิก',
                reverseButtons: true,
                focusCancel: options.focusCancel !== false,
                buttonsStyling: false,
                customClass: {
                    popup: 'font-thai rounded-2xl',
                    actions: 'gap-3',
                    confirmButton: options.danger ? dangerConfirmClass : baseConfirmClass,
                    cancelButton: cancelClass
                },
                returnFocus: true,
                heightAuto: false
            });
            return result.isConfirmed;
        },

        async alert(message, options = {}) {
            const swal = getSwal();
            if (!swal) return;
            await swal.fire({
                title: options.title || (options.icon === 'error' ? 'ไม่สามารถดำเนินการได้' : 'แจ้งให้ทราบ'),
                text: message,
                icon: options.icon || 'info',
                confirmButtonText: options.confirmText || 'รับทราบ',
                buttonsStyling: false,
                customClass: {
                    popup: 'font-thai rounded-2xl',
                    confirmButton: baseConfirmClass
                },
                returnFocus: true,
                heightAuto: false
            });
        },

        async secret(options = {}) {
            const swal = getSwal();
            if (!swal) return;
            const escapeHtml = value => {
                const node = document.createElement('div');
                node.textContent = value || '';
                return node.innerHTML;
            };
            const password = String(options.value || '');
            await swal.fire({
                title: options.title || 'รหัสผ่านชั่วคราว',
                html: `
                    <p class="mb-3 text-sm text-slate-600">${escapeHtml(options.target)}</p>
                    <p class="mb-3 text-xs text-slate-500">${escapeHtml(options.description || 'กรุณาส่งรหัสนี้ผ่านช่องทางที่ปลอดภัย และปิดหน้าต่างเมื่อบันทึกเรียบร้อย')}</p>
                    <div class="mb-3 rounded-xl border-2 border-dashed border-amber-300 bg-amber-50 p-4">
                        <code class="select-all break-all text-2xl font-bold tracking-widest text-slate-900">${escapeHtml(password)}</code>
                    </div>
                    <p class="mb-4 text-xs font-medium text-red-700">หมดอายุ: ${escapeHtml(options.expires)}</p>
                    <button type="button" data-copy-secret class="inline-flex min-h-11 w-full items-center justify-center rounded-lg bg-blue-700 px-4 py-2.5 text-sm font-semibold text-white hover:bg-blue-800 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-700">${escapeHtml(options.copyText || 'คัดลอกรหัสผ่าน')}</button>`,
                icon: 'warning',
                confirmButtonText: 'รับทราบและปิด',
                allowOutsideClick: false,
                buttonsStyling: false,
                customClass: {
                    popup: 'font-thai rounded-2xl',
                    confirmButton: baseConfirmClass
                },
                didOpen: popup => {
                    popup.querySelector('[data-copy-secret]')?.addEventListener('click', async event => {
                        await navigator.clipboard.writeText(password);
                        event.currentTarget.textContent = 'คัดลอกแล้ว';
                    });
                },
                returnFocus: true,
                heightAuto: false
            });
        },

        toast(message, icon = 'success') {
            const swal = getSwal();
            if (!swal || !message) return;
            swal.fire({
                toast: true,
                position: 'top-end',
                icon,
                title: message,
                showConfirmButton: false,
                timer: icon === 'error' ? 6000 : 4000,
                timerProgressBar: true,
                showCloseButton: true,
                customClass: { popup: 'font-thai' },
                didOpen: popup => {
                    popup.addEventListener('mouseenter', swal.stopTimer);
                    popup.addEventListener('mouseleave', swal.resumeTimer);
                }
            });
        },

        loading(title = 'กำลังดำเนินการ…') {
            const swal = getSwal();
            if (!swal) return;
            swal.fire({
                title,
                allowEscapeKey: false,
                allowOutsideClick: false,
                showConfirmButton: false,
                customClass: { popup: 'font-thai rounded-2xl' },
                didOpen: () => swal.showLoading(),
                heightAuto: false
            });
        },

        close() {
            getSwal()?.close();
        }
    };

    document.addEventListener('submit', async event => {
        const form = event.target;
        if (!(form instanceof HTMLFormElement)) return;
        if (approvedForms.has(form)) {
            approvedForms.delete(form);
            return;
        }

        const submitter = event.submitter;
        const source = submitter?.dataset.confirm ? submitter : form;
        const message = source.dataset.confirm;
        if (!message) return;

        event.preventDefault();
        const confirmed = await window.SrtDialog.confirm(message, {
            title: source.dataset.confirmTitle,
            confirmText: source.dataset.confirmText,
            cancelText: source.dataset.cancelText,
            icon: source.dataset.confirmIcon,
            danger: source.dataset.confirmDanger === 'true'
        });
        if (!confirmed) return;

        approvedForms.add(form);
        if (submitter instanceof HTMLElement) form.requestSubmit(submitter);
        else form.requestSubmit();
    });

    document.addEventListener('DOMContentLoaded', () => {
        document.querySelectorAll('[data-swal-toast]').forEach(element => {
            window.SrtDialog.toast(element.textContent.trim(), element.dataset.swalToast || 'success');
            element.remove();
        });
    });

    // Render any remaining legacy alert call through the shared accessible dialog.
    window.alert = message => window.SrtDialog.alert(String(message));
})();
// ─────────────────────────────────────────────────────────────────────────────

// Auto-dismiss flash messages only. Validation messages also use role="alert"
// and must remain in the DOM until the user fixes the related field.
document.addEventListener('DOMContentLoaded', () => {
    document.querySelectorAll('[data-auto-dismiss="true"]').forEach(el => {
        setTimeout(() => {
            el.style.transition = 'opacity 0.4s';
            el.style.opacity = '0';
            setTimeout(() => el.remove(), 400);
        }, 6000);
    });
});

// HTMX: re-run alert dismiss on every htmx:afterSwap
document.addEventListener('htmx:afterSwap', (e) => {
    e.detail.elt.querySelectorAll('[data-auto-dismiss="true"]').forEach(el => {
        setTimeout(() => {
            el.style.transition = 'opacity 0.4s';
            el.style.opacity = '0';
            setTimeout(() => el.remove(), 400);
        }, 6000);
    });
});
