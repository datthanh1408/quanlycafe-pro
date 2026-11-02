/* ==========================================================================
   STAFF POS CLIENT ENGINE (/staff/pos)
   Hỗ trợ chọn món chạm nhanh >= 48px, tùy chỉnh Size/Nóng-Lạnh/Đường/Đá/Topping,
   chọn bàn trên Sơ đồ 3D hoặc Gọi thêm món vào đơn bàn đang mở, tính tiền thừa, QR
   ========================================================================== */
(function () {
    const fmtVnd = (n) => new Intl.NumberFormat('vi-VN').format(Math.round(n || 0)) + ' ₫';

    window.initPosTerminal = function (config) {
        const products = config.products || [];
        const canApplyPromo = !!config.canApplyPromo;
        const csrfToken = config.csrfToken || '';

        let cart = [];
        let activeCategory = 'ALL';
        let searchKeyword = '';
        let appliedPromo = null; // { code, discountAmount, message }
        let editingProduct = null;

        // DOM Elements
        const gridEl = document.getElementById('posProductGrid');
        const searchEl = document.getElementById('posSearchInput');
        const cartListEl = document.getElementById('posCartList');
        const subtotalEl = document.getElementById('posSubtotal');
        const discountEl = document.getElementById('posDiscount');
        const totalEl = document.getElementById('posTotal');
        const orderTypeEl = document.getElementById('posOrderType');
        const tableSelectWrap = document.getElementById('posTableSelectWrap');
        const tableSelectEl = document.getElementById('posTableId');
        const openTableBanner = document.getElementById('posOpenTableBanner');
        const paymentMethodEl = document.getElementById('posPaymentMethod');
        const cashCalcWrap = document.getElementById('posCashCalcWrap');
        const cashGivenEl = document.getElementById('posCashGiven');
        const cashChangeEl = document.getElementById('posCashChange');
        const qrPreviewWrap = document.getElementById('posQrPreviewWrap');
        const markPaidCheck = document.getElementById('posMarkPaid');

        // Modal tùy chỉnh món
        const modalEl = document.getElementById('posItemModal');
        const bsModal = modalEl ? new bootstrap.Modal(modalEl) : null;

        function getTotals() {
            const subtotal = cart.reduce((s, item) => s + item.lineTotal, 0);
            const discount = appliedPromo ? Math.min(appliedPromo.discountAmount, subtotal) : 0;
            const total = Math.max(0, subtotal - discount);
            return { subtotal, discount, total };
        }

        function renderProducts() {
            if (!gridEl) return;
            const kw = searchKeyword.trim().toLowerCase();
            const filtered = products.filter(p => {
                if (activeCategory !== 'ALL' && String(p.categoryId) !== String(activeCategory)) return false;
                if (kw && !p.name.toLowerCase().includes(kw)) return false;
                return true;
            });

            if (filtered.length === 0) {
                gridEl.innerHTML = `<div class="col-12 text-center py-5 text-muted">
                    <i class="bi bi-search fs-1 d-block mb-2"></i>Không tìm thấy món phù hợp.
                </div>`;
                return;
            }

            gridEl.innerHTML = filtered.map(p => `
                <div class="col-6 col-md-4 col-xl-3">
                    <div class="pos-product-tile p-2 h-100 d-flex flex-column justify-content-between ${!p.isAvailable ? 'opacity-50' : ''}"
                         data-product-id="${p.id}">
                        <div>
                            <div class="ratio ratio-4x3 rounded-3 overflow-hidden mb-2 bg-cream position-relative">
                                <img src="${p.imageUrl}" alt="${p.name}" class="object-fit-cover w-100 h-100" loading="lazy" />
                                ${!p.isAvailable ? '<span class="badge bg-danger position-absolute top-50 start-50 translate-middle">Tạm hết</span>' : ''}
                            </div>
                            <div class="fw-bold small lh-sm mb-1">${p.name}</div>
                        </div>
                        <div class="d-flex align-items-center justify-content-between mt-1">
                            <span class="text-coffee fw-bold small">${fmtVnd(p.basePrice)}</span>
                            <span class="btn btn-sm btn-coffee py-0 px-2" style="min-height:30px;">+</span>
                        </div>
                    </div>
                </div>
            `).join('');

            gridEl.querySelectorAll('.pos-product-tile').forEach(tile => {
                tile.addEventListener('click', () => {
                    const pid = parseInt(tile.dataset.productId, 10);
                    const prod = products.find(x => x.id === pid);
                    if (!prod) return;
                    if (!prod.isAvailable) {
                        window.DatMocRealtime?.showToast('Tạm hết hàng', `Món "${prod.name}" hiện đang tạm hết.`, 'warning');
                        return;
                    }
                    openCustomizeModal(prod);
                });
            });
        }

        function openCustomizeModal(prod) {
            editingProduct = prod;
            document.getElementById('posModalTitle').textContent = prod.name;

            // Sizes
            const sizeWrap = document.getElementById('posModalSizes');
            if (prod.sizes && prod.sizes.length > 0) {
                sizeWrap.innerHTML = prod.sizes.map((s, idx) => `
                    <input type="radio" class="btn-check" name="posSize" id="posSize_${idx}" value="${s.sizeName}" data-price="${s.price}" ${idx === 0 ? 'checked' : ''}>
                    <label class="btn btn-outline-coffee staff-touch-btn px-3" for="posSize_${idx}">
                        ${s.sizeName} (${fmtVnd(s.price)})
                    </label>
                `).join('');
            } else {
                sizeWrap.innerHTML = `<span class="badge bg-secondary">${fmtVnd(prod.basePrice)}</span>`;
            }

            // Options Nóng/Lạnh, Đường, Đá
            document.getElementById('posModalTempGroup').style.display = prod.allowTemperature ? 'block' : 'none';
            document.getElementById('posModalSugarGroup').style.display = prod.allowSugar ? 'block' : 'none';
            document.getElementById('posModalIceGroup').style.display = prod.allowIce ? 'block' : 'none';

            // Toppings
            const topWrap = document.getElementById('posModalToppings');
            const topSection = document.getElementById('posModalToppingSection');
            if (prod.toppings && prod.toppings.length > 0) {
                topSection.style.display = 'block';
                topWrap.innerHTML = prod.toppings.map(t => `
                    <div class="col-6">
                        <label class="border rounded-3 p-2 d-flex align-items-center gap-2 w-100" style="min-height:48px; cursor:pointer;">
                            <input type="checkbox" class="form-check-input m-0 pos-top-check" value="${t.id}" data-name="${t.name}" data-price="${t.price}">
                            <span class="small flex-grow-1">${t.name}</span>
                            <strong class="small text-coffee">+${fmtVnd(t.price)}</strong>
                        </label>
                    </div>
                `).join('');
            } else {
                topSection.style.display = 'none';
                topWrap.innerHTML = '';
            }

            document.getElementById('posModalQty').value = 1;
            document.getElementById('posModalNote').value = '';
            updateModalPreviewPrice();

            modalEl.querySelectorAll('input').forEach(inp => {
                inp.addEventListener('change', updateModalPreviewPrice);
            });

            bsModal?.show();
        }

        function updateModalPreviewPrice() {
            if (!editingProduct) return;
            const checkedSize = modalEl.querySelector('input[name="posSize"]:checked');
            let base = checkedSize ? parseFloat(checkedSize.dataset.price) : editingProduct.basePrice;
            let topSum = 0;
            modalEl.querySelectorAll('.pos-top-check:checked').forEach(chk => {
                topSum += parseFloat(chk.dataset.price || '0');
            });
            const qty = Math.max(1, parseInt(document.getElementById('posModalQty').value || '1', 10));
            document.getElementById('posModalAddPrice').textContent = fmtVnd((base + topSum) * qty);
        }

        document.getElementById('posModalConfirmBtn')?.addEventListener('click', () => {
            if (!editingProduct) return;
            const checkedSize = modalEl.querySelector('input[name="posSize"]:checked');
            const sizeName = checkedSize ? checkedSize.value : null;
            const sizePrice = checkedSize ? parseFloat(checkedSize.dataset.price) : editingProduct.basePrice;

            const temp = editingProduct.allowTemperature
                ? (modalEl.querySelector('input[name="posTemp"]:checked')?.value || 'Lạnh')
                : null;
            const sugar = editingProduct.allowSugar
                ? (modalEl.querySelector('input[name="posSugar"]:checked')?.value || '100%')
                : null;
            const ice = (editingProduct.allowIce && temp !== 'Nóng')
                ? (modalEl.querySelector('input[name="posIce"]:checked')?.value || '100%')
                : null;

            const toppings = [];
            modalEl.querySelectorAll('.pos-top-check:checked').forEach(chk => {
                toppings.push({
                    id: parseInt(chk.value, 10),
                    name: chk.dataset.name,
                    price: parseFloat(chk.dataset.price)
                });
            });

            const qty = Math.min(99, Math.max(1, parseInt(document.getElementById('posModalQty').value || '1', 10)));
            const note = document.getElementById('posModalNote').value.trim();
            const unitPrice = sizePrice + toppings.reduce((s, x) => s + x.price, 0);

            cart.push({
                uid: Date.now() + '_' + Math.random().toString(36).slice(2, 6),
                productId: editingProduct.id,
                productName: editingProduct.name,
                sizeName,
                temperature: temp,
                sugarLevel: sugar,
                iceLevel: ice,
                toppings,
                unitPrice,
                quantity: qty,
                lineTotal: unitPrice * qty,
                note
            });

            bsModal?.hide();
            revalidatePromoIfAny();
            renderCart();
        });

        function renderCart() {
            if (!cartListEl) return;
            if (cart.length === 0) {
                cartListEl.innerHTML = `<div class="text-center text-muted py-4 small">
                    <i class="bi bi-basket2 fs-2 d-block mb-1"></i>Chưa có món trong đơn hiện tại
                </div>`;
            } else {
                cartListEl.innerHTML = cart.map(item => {
                    const opts = [item.sizeName ? `Size ${item.sizeName}` : null, item.temperature, item.sugarLevel ? `Đường ${item.sugarLevel}` : null, item.iceLevel ? `Đá ${item.iceLevel}` : null]
                        .filter(Boolean).join(' • ');
                    const tops = item.toppings.map(t => t.name).join(', ');
                    return `
                        <div class="border-bottom py-2">
                            <div class="d-flex justify-content-between align-items-start">
                                <div>
                                    <div class="fw-bold small">${item.productName}</div>
                                    ${opts ? `<div class="text-muted" style="font-size:0.75rem;">${opts}</div>` : ''}
                                    ${tops ? `<div class="text-coffee" style="font-size:0.75rem;">+ ${tops}</div>` : ''}
                                    ${item.note ? `<div class="fst-italic text-secondary" style="font-size:0.74rem;">"${item.note}"</div>` : ''}
                                </div>
                                <strong class="small text-nowrap ms-2">${fmtVnd(item.lineTotal)}</strong>
                            </div>
                            <div class="d-flex align-items-center justify-content-between mt-1">
                                <span class="text-muted" style="font-size:0.75rem;">${fmtVnd(item.unitPrice)} / món</span>
                                <div class="btn-group btn-group-sm">
                                    <button type="button" class="btn btn-outline-secondary px-2" data-act="dec" data-uid="${item.uid}">-</button>
                                    <span class="btn btn-light disabled fw-bold px-2">${item.quantity}</span>
                                    <button type="button" class="btn btn-outline-secondary px-2" data-act="inc" data-uid="${item.uid}">+</button>
                                    <button type="button" class="btn btn-outline-danger px-2" data-act="del" data-uid="${item.uid}"><i class="bi bi-trash"></i></button>
                                </div>
                            </div>
                        </div>`;
                }).join('');

                cartListEl.querySelectorAll('button[data-act]').forEach(btn => {
                    btn.addEventListener('click', () => {
                        const uid = btn.dataset.uid;
                        const act = btn.dataset.act;
                        const idx = cart.findIndex(x => x.uid === uid);
                        if (idx < 0) return;
                        if (act === 'inc' && cart[idx].quantity < 99) {
                            cart[idx].quantity++;
                            cart[idx].lineTotal = cart[idx].unitPrice * cart[idx].quantity;
                        } else if (act === 'dec') {
                            if (cart[idx].quantity > 1) {
                                cart[idx].quantity--;
                                cart[idx].lineTotal = cart[idx].unitPrice * cart[idx].quantity;
                            } else {
                                cart.splice(idx, 1);
                            }
                        } else if (act === 'del') {
                            cart.splice(idx, 1);
                        }
                        revalidatePromoIfAny();
                        renderCart();
                    });
                });
            }

            const { subtotal, discount, total } = getTotals();
            if (subtotalEl) subtotalEl.textContent = fmtVnd(subtotal);
            if (discountEl) discountEl.textContent = discount > 0 ? `-${fmtVnd(discount)}` : '0 ₫';
            if (totalEl) totalEl.textContent = fmtVnd(total);
            updateCashChange();
        }

        function updateCashChange() {
            if (!cashGivenEl || !cashChangeEl) return;
            const { total } = getTotals();
            const given = parseFloat(cashGivenEl.value || '0') || 0;
            const change = given - total;
            cashChangeEl.textContent = given > 0 ? (change >= 0 ? fmtVnd(change) : `Thiếu ${fmtVnd(-change)}`) : '0 ₫';
            cashChangeEl.className = change < 0 && given > 0 ? 'fw-bold text-danger' : 'fw-bold text-success';
        }

        async function revalidatePromoIfAny() {
            if (!appliedPromo) return;
            const { subtotal } = getTotals();
            if (subtotal <= 0) {
                appliedPromo = null;
                const msgEl = document.getElementById('posPromoMessage');
                if (msgEl) msgEl.textContent = '';
                return;
            }
            await applyPromoCode(appliedPromo.code, true);
        }

        async function applyPromoCode(code, silent = false) {
            if (!canApplyPromo) return;
            const { subtotal } = getTotals();
            const msgEl = document.getElementById('posPromoMessage');
            if (!code || subtotal <= 0) {
                appliedPromo = null;
                if (msgEl) msgEl.textContent = '';
                renderCart();
                return;
            }

            const res = await fetch('/staff/pos/validate-promo', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': csrfToken
                },
                body: JSON.stringify({ code, subtotal })
            });
            const data = await res.json();
            if (data.isValid) {
                appliedPromo = { code: data.code, discountAmount: data.discountAmount, message: data.message };
                if (msgEl) {
                    msgEl.className = 'small text-success mt-1';
                    msgEl.textContent = data.message;
                }
            } else {
                appliedPromo = null;
                if (msgEl && !silent) {
                    msgEl.className = 'small text-danger mt-1';
                    msgEl.textContent = data.message;
                }
            }
            renderCart();
        }

        // Category Tabs & Search
        document.querySelectorAll('[data-pos-cat]').forEach(btn => {
            btn.addEventListener('click', () => {
                document.querySelectorAll('[data-pos-cat]').forEach(b => b.classList.remove('btn-coffee'));
                document.querySelectorAll('[data-pos-cat]').forEach(b => b.classList.add('btn-outline-coffee'));
                btn.classList.remove('btn-outline-coffee');
                btn.classList.add('btn-coffee');
                activeCategory = btn.dataset.posCat;
                renderProducts();
            });
        });

        searchEl?.addEventListener('input', () => {
            searchKeyword = searchEl.value;
            renderProducts();
        });

        orderTypeEl?.addEventListener('change', () => {
            const isDineIn = orderTypeEl.value === '0';
            if (tableSelectWrap) tableSelectWrap.style.display = isDineIn ? 'block' : 'none';
            checkSelectedTableOpenOrder();
        });

        tableSelectEl?.addEventListener('change', checkSelectedTableOpenOrder);

        function checkSelectedTableOpenOrder() {
            if (!tableSelectEl || !openTableBanner) return;
            const opt = tableSelectEl.options[tableSelectEl.selectedIndex];
            const openOrderId = opt?.dataset?.openOrderId;
            const openOrderCode = opt?.dataset?.openOrderCode;
            if (orderTypeEl?.value === '0' && openOrderId) {
                openTableBanner.style.display = 'block';
                openTableBanner.innerHTML = `<i class="bi bi-info-circle-fill me-1"></i> Bàn này đang có đơn mở <strong>${openOrderCode}</strong>. Bấm "Xác nhận" sẽ <strong>gọi thêm món</strong> vào đơn mở này (giữ đơn mở tại bàn).`;
                if (markPaidCheck) markPaidCheck.checked = false;
            } else {
                openTableBanner.style.display = 'none';
            }
        }

        paymentMethodEl?.addEventListener('change', () => {
            const val = paymentMethodEl.value;
            if (cashCalcWrap) cashCalcWrap.style.display = val === 'CASH' ? 'block' : 'none';
            if (qrPreviewWrap) qrPreviewWrap.style.display = val === 'QR' ? 'block' : 'none';
        });

        cashGivenEl?.addEventListener('input', updateCashChange);

        document.getElementById('posApplyPromoBtn')?.addEventListener('click', () => {
            const code = document.getElementById('posPromoInput')?.value?.trim();
            applyPromoCode(code, false);
        });

        document.getElementById('posClearCartBtn')?.addEventListener('click', () => {
            cart = [];
            appliedPromo = null;
            const msgEl = document.getElementById('posPromoMessage');
            if (msgEl) msgEl.textContent = '';
            renderCart();
        });

        // Submit POS Order
        document.getElementById('posSubmitBtn')?.addEventListener('click', async (ev) => {
            const btn = ev.currentTarget;
            if (cart.length === 0) {
                window.DatMocRealtime?.showToast('Chưa chọn món', 'Vui lòng chọn ít nhất 1 món vào đơn POS.', 'warning');
                return;
            }

            const isDineIn = orderTypeEl?.value === '0';
            const selectedOpt = tableSelectEl?.options[tableSelectEl.selectedIndex];
            const tableId = isDineIn && tableSelectEl?.value ? parseInt(tableSelectEl.value, 10) : null;
            const openOrderId = isDineIn && selectedOpt?.dataset?.openOrderId ? parseInt(selectedOpt.dataset.openOrderId, 10) : null;

            if (isDineIn && !tableId) {
                window.DatMocRealtime?.showToast('Chưa chọn bàn', 'Vui lòng chọn bàn phục vụ hoặc chuyển sang Mang đi.', 'warning');
                return;
            }

            btn.disabled = true;
            try {
                const payload = {
                    orderType: isDineIn ? 0 : 1,
                    tableId,
                    appendToOpenOrderId: openOrderId,
                    customerName: document.getElementById('posCustomerName')?.value?.trim() || 'Khách tại quầy',
                    phone: document.getElementById('posCustomerPhone')?.value?.trim() || '',
                    paymentMethod: paymentMethodEl?.value || 'CASH',
                    markPaidImmediately: markPaidCheck ? markPaidCheck.checked : false,
                    promoCode: appliedPromo?.code || null,
                    note: document.getElementById('posOrderNote')?.value?.trim() || '',
                    lines: cart.map(c => ({
                        productId: c.productId,
                        sizeName: c.sizeName,
                        temperature: c.temperature,
                        sugarLevel: c.sugarLevel,
                        iceLevel: c.iceLevel,
                        toppingIds: c.toppings.map(t => t.id),
                        quantity: c.quantity,
                        note: c.note
                    }))
                };

                const res = await fetch('/staff/pos/submit', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': csrfToken
                    },
                    body: JSON.stringify(payload)
                });
                const result = await res.json();
                if (!result.success) {
                    window.DatMocRealtime?.showToast('Không thể lưu đơn', result.errorMessage || 'Vui lòng kiểm tra lại.', 'danger');
                    return;
                }

                window.DatMocRealtime?.showToast('Thành công!', `Đã lưu đơn ${result.orderCode}.`, 'success');
                window.location.href = `/staff/orders/receipt/${result.orderId}?fromPos=true`;
            } catch (err) {
                window.DatMocRealtime?.showToast('Lỗi kết nối', 'Không thể gửi đơn POS lên máy chủ.', 'danger');
            } finally {
                btn.disabled = false;
            }
        });

        // Modal giảm/tăng số lượng
        document.getElementById('posQtyMinus')?.addEventListener('click', () => {
            const el = document.getElementById('posModalQty');
            el.value = Math.max(1, (parseInt(el.value, 10) || 1) - 1);
            updateModalPreviewPrice();
        });
        document.getElementById('posQtyPlus')?.addEventListener('click', () => {
            const el = document.getElementById('posModalQty');
            el.value = Math.min(99, (parseInt(el.value, 10) || 1) + 1);
            updateModalPreviewPrice();
        });

        renderProducts();
        renderCart();
        checkSelectedTableOpenOrder();
    };
})();

