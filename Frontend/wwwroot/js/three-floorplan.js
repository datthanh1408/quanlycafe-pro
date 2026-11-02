/* ==========================================================================
   THREE.JS MODULE 3: SƠ ĐỒ BÀN 3D THỜI GIAN THỰC & TRÌNH CHỈNH SỬA KÉO-THẢ 3D
   Dùng tại: /staff/tables, /staff/pos, /admin/tables (Editable Drag), /checkout (Chọn bàn)
   ========================================================================== */
import * as THREE from '../lib/three/three.module.min.js';
import { OrbitControls } from '../lib/three/OrbitControls.js';

export function initFloorPlan3D(options) {
    const {
        containerId,
        tooltipId,
        tables = [],
        editable = false,
        onlySelectAvailable = false,
        initialSelectedId = null,
        onSelectTable = () => {},
        onPositionChanged = () => {}
    } = options;

    const container = document.getElementById(containerId);
    const tooltip = tooltipId ? document.getElementById(tooltipId) : null;
    if (!container) return null;

    const scene = new THREE.Scene();
    scene.background = new THREE.Color(0x1b1512);

    const camera = new THREE.PerspectiveCamera(42, container.clientWidth / container.clientHeight, 0.1, 100);
    camera.position.set(0, 14.5, 13.8);

    const renderer = new THREE.WebGLRenderer({ antialias: true });
    renderer.setSize(container.clientWidth, container.clientHeight);
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    renderer.shadowMap.enabled = true;
    container.appendChild(renderer.domElement);

    const controls = new OrbitControls(camera, renderer.domElement);
    controls.enableDamping = true;
    controls.dampingFactor = 0.08;
    controls.maxPolarAngle = Math.PI / 2.12;
    controls.minDistance = 5;
    controls.maxDistance = 28;
    controls.target.set(0, 0, 1.2);

    // Ánh sáng rõ nét cho điều hành POS & quản lý bàn
    scene.add(new THREE.AmbientLight(0xfff3e6, 0.95));
    const dirLight = new THREE.DirectionalLight(0xffffff, 1.25);
    dirLight.position.set(6, 16, 10);
    dirLight.castShadow = true;
    scene.add(dirLight);

    // Sàn phân vùng trực quan 3 khu vực: Tầng 1 (Trái), Tầng 2 (Phải trên), Sân vườn (Phải dưới)
    function createZoneSlab(w, d, x, z, colorHex, labelText) {
        const slab = new THREE.Mesh(
            new THREE.BoxGeometry(w, 0.24, d),
            new THREE.MeshStandardMaterial({ color: colorHex, roughness: 0.8 })
        );
        slab.position.set(x, -0.12, z);
        slab.receiveShadow = true;
        scene.add(slab);

        // Nhãn tên khu vực in trên mặt sàn bằng CanvasTexture
        const cnv = document.createElement('canvas');
        cnv.width = 512;
        cnv.height = 128;
        const ctx = cnv.getContext('2d');
        ctx.fillStyle = 'rgba(255,255,255,0.14)';
        ctx.font = 'bold 46px "Segoe UI", sans-serif';
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        ctx.fillText(labelText.toUpperCase(), 256, 64);

        const tex = new THREE.CanvasTexture(cnv);
        const labelPlane = new THREE.Mesh(
            new THREE.PlaneGeometry(4.2, 1.05),
            new THREE.MeshBasicMaterial({ map: tex, transparent: true, depthWrite: false })
        );
        labelPlane.rotation.x = -Math.PI / 2;
        labelPlane.position.set(x, 0.015, z - d / 2 + 0.8);
        scene.add(labelPlane);
    }

    createZoneSlab(8.2, 13.5, -4.2, 1.2, 0x4d3423, 'Tầng 1');
    createZoneSlab(8.2, 6.6, 4.3, -2.2, 0x5f432c, 'Tầng 2');
    createZoneSlab(8.2, 6.5, 4.3, 4.7, 0x2c442e, 'Sân vườn');

    // Quầy Bar định hướng ở đầu Tầng 1
    const barLandmark = new THREE.Mesh(
        new THREE.BoxGeometry(5.6, 0.95, 1.1),
        new THREE.MeshStandardMaterial({ color: 0x2b190f, roughness: 0.5 })
    );
    barLandmark.position.set(-4.2, 0.48, -4.9);
    scene.add(barLandmark);

    const gridHelper = new THREE.GridHelper(20, 20, 0x6f4e37, 0x332922);
    gridHelper.position.set(0, 0.005, 1.2);
    gridHelper.visible = editable;
    scene.add(gridHelper);

    // Màu chuẩn Design System Mục 9: Trống (#10B981), Đang sử dụng (#F59E0B), Đã đặt trước (#3B82F6)
    const STATUS_COLORS = {
        0: 0x10b981,
        1: 0xf59e0b,
        2: 0x3b82f6
    };

    const STATUS_CSS = {
        0: '#10B981',
        1: '#F59E0B',
        2: '#3B82F6'
    };

    let currentTables = Array.isArray(tables) ? [...tables] : [];
    let selectedTableId = initialSelectedId;
    let dragModeEnabled = false;
    const tableGroups = new Map(); // tableId -> THREE.Group
    const pickableHitboxes = [];

    function makeBadgeSprite(code, capacity, status) {
        const cnv = document.createElement('canvas');
        cnv.width = 256;
        cnv.height = 112;
        const ctx = cnv.getContext('2d');

        ctx.fillStyle = 'rgba(20, 15, 11, 0.88)';
        ctx.beginPath();
        ctx.roundRect(8, 8, 240, 96, 20);
        ctx.fill();

        ctx.lineWidth = 5;
        ctx.strokeStyle = STATUS_CSS[status] || '#10B981';
        ctx.stroke();

        ctx.fillStyle = '#FFFFFF';
        ctx.font = 'bold 38px "Segoe UI", sans-serif';
        ctx.textAlign = 'center';
        ctx.fillText(code, 128, 52);

        ctx.fillStyle = '#E2D6C8';
        ctx.font = '24px "Segoe UI", sans-serif';
        ctx.fillText(`${capacity} khách`, 128, 84);

        const tex = new THREE.CanvasTexture(cnv);
        const sprite = new THREE.Sprite(new THREE.SpriteMaterial({ map: tex, transparent: true }));
        sprite.scale.set(1.55, 0.68, 1);
        sprite.position.set(0, 1.52, 0);
        return sprite;
    }

    function buildTablesScene() {
        tableGroups.forEach(g => scene.remove(g));
        tableGroups.clear();
        pickableHitboxes.length = 0;

        const woodChairMat = new THREE.MeshStandardMaterial({ color: 0x3d2516, roughness: 0.6 });

        currentTables.forEach(t => {
            const group = new THREE.Group();
            group.position.set(t.posX || 0, 0, t.posY || 0);
            group.userData = { table: t };

            const radius = t.capacity >= 6 ? 0.88 : (t.capacity <= 2 ? 0.56 : 0.72);
            const statusHex = STATUS_COLORS[t.status] ?? 0x10b981;
            const isSelected = selectedTableId === t.id;

            // Mặt bàn
            const topMat = new THREE.MeshStandardMaterial({
                color: isSelected ? 0xfff3d6 : 0xf5efe8,
                roughness: 0.3
            });
            const top = new THREE.Mesh(new THREE.CylinderGeometry(radius, radius, 0.08, 28), topMat);
            top.position.y = 0.78;
            top.castShadow = true;
            group.add(top);

            // Viền màu trạng thái quanh mặt bàn
            const rim = new THREE.Mesh(
                new THREE.TorusGeometry(radius + 0.02, 0.04, 12, 32),
                new THREE.MeshBasicMaterial({ color: statusHex })
            );
            rim.rotation.x = Math.PI / 2;
            rim.position.y = 0.78;
            group.add(rim);

            // Chân bàn
            const leg = new THREE.Mesh(new THREE.CylinderGeometry(0.09, 0.25, 0.76, 16), woodChairMat);
            leg.position.y = 0.38;
            group.add(leg);

            // Vòng halo dưới sàn (vàng kim nếu đang được chọn)
            const floorHalo = new THREE.Mesh(
                new THREE.RingGeometry(radius * 0.85, radius * (isSelected ? 1.25 : 1.08), 32),
                new THREE.MeshBasicMaterial({
                    color: isSelected ? 0xfbbf24 : statusHex,
                    side: THREE.DoubleSide,
                    transparent: true,
                    opacity: isSelected ? 0.95 : 0.65
                })
            );
            floorHalo.rotation.x = -Math.PI / 2;
            floorHalo.position.y = 0.02;
            group.add(floorHalo);

            // Ghế theo đúng sức chứa bàn
            const seats = Math.min(Math.max(t.capacity || 4, 2), 8);
            for (let i = 0; i < seats; i++) {
                const a = (i / seats) * Math.PI * 2;
                const chair = new THREE.Mesh(new THREE.BoxGeometry(0.36, 0.44, 0.36), woodChairMat);
                chair.position.set(Math.cos(a) * (radius + 0.32), 0.22, Math.sin(a) * (radius + 0.32));
                chair.rotation.y = -a;
                chair.castShadow = true;
                group.add(chair);
            }

            // Đạo cụ 3D trực quan theo trạng thái bàn
            if (t.status === 1) {
                // Bàn Đang sử dụng: đặt 2 ly cà phê trên mặt bàn
                const cup1 = new THREE.Mesh(
                    new THREE.CylinderGeometry(0.1, 0.08, 0.18, 14),
                    new THREE.MeshStandardMaterial({ color: 0x6f4e37 })
                );
                cup1.position.set(-0.16, 0.91, 0.08);
                group.add(cup1);

                const cup2 = new THREE.Mesh(
                    new THREE.CylinderGeometry(0.1, 0.08, 0.18, 14),
                    new THREE.MeshStandardMaterial({ color: 0xd97706 })
                );
                cup2.position.set(0.18, 0.91, -0.1);
                group.add(cup2);
            } else if (t.status === 2) {
                // Bàn Đã đặt trước: biển tam giác Reserved màu xanh
                const tent = new THREE.Mesh(
                    new THREE.ConeGeometry(0.16, 0.24, 4),
                    new THREE.MeshStandardMaterial({ color: 0x3b82f6 })
                );
                tent.position.set(0, 0.94, 0);
                group.add(tent);
            }

            // Nhãn mã bàn nổi phía trên
            group.add(makeBadgeSprite(t.code, t.capacity, t.status));

            // Hitbox vô hình giúp click/chạm mượt trên máy tính bảng
            const hitbox = new THREE.Mesh(
                new THREE.CylinderGeometry(radius + 0.42, radius + 0.42, 1.6, 16),
                new THREE.MeshBasicMaterial({ visible: false })
            );
            hitbox.position.y = 0.8;
            hitbox.userData = { tableId: t.id };
            group.add(hitbox);
            pickableHitboxes.push(hitbox);

            scene.add(group);
            tableGroups.set(t.id, group);
        });
    }

    buildTablesScene();

    // Raycasting cho Hover, Click & Kéo-Thả Admin 3D
    const raycaster = new THREE.Raycaster();
    const pointer = new THREE.Vector2();
    const dragPlane = new THREE.Plane(new THREE.Vector3(0, 1, 0), 0);
    const dragIntersect = new THREE.Vector3();
    let draggingTableId = null;
    let pointerDownPos = { x: 0, y: 0 };

    function updatePointer(ev) {
        const rect = renderer.domElement.getBoundingClientRect();
        pointer.x = ((ev.clientX - rect.left) / rect.width) * 2 - 1;
        pointer.y = -((ev.clientY - rect.top) / rect.height) * 2 + 1;
        return rect;
    }

    function detectZoneByCoords(x, z) {
        if (x < 0) return 'Tầng 1';
        return z < 1.3 ? 'Tầng 2' : 'Sân vườn';
    }

    renderer.domElement.addEventListener('pointerdown', (ev) => {
        pointerDownPos = { x: ev.clientX, y: ev.clientY };
        updatePointer(ev);
        raycaster.setFromCamera(pointer, camera);
        const hits = raycaster.intersectObjects(pickableHitboxes, false);

        if (hits.length > 0 && editable && dragModeEnabled) {
            draggingTableId = hits[0].object.userData.tableId;
            controls.enabled = false;
            renderer.domElement.style.cursor = 'grabbing';
        }
    });

    renderer.domElement.addEventListener('pointermove', (ev) => {
        const rect = updatePointer(ev);
        raycaster.setFromCamera(pointer, camera);

        if (draggingTableId !== null && editable && dragModeEnabled) {
            if (raycaster.ray.intersectPlane(dragPlane, dragIntersect)) {
                // Snap to grid 0.2m
                const snappedX = Math.round(Math.max(-7.6, Math.min(7.6, dragIntersect.x)) * 5) / 5;
                const snappedZ = Math.round(Math.max(-4.8, Math.min(7.2, dragIntersect.z)) * 5) / 5;
                const grp = tableGroups.get(draggingTableId);
                if (grp) {
                    grp.position.set(snappedX, 0, snappedZ);
                }
            }
            return;
        }

        const hits = raycaster.intersectObjects(pickableHitboxes, false);
        if (hits.length > 0) {
            const tId = hits[0].object.userData.tableId;
            const t = currentTables.find(x => x.id === tId);
            renderer.domElement.style.cursor = (editable && dragModeEnabled) ? 'grab' : 'pointer';

            if (tooltip && t) {
                let extra = '';
                if (t.activeOrderCode) extra = ` • Đơn: ${t.activeOrderCode}`;
                else if (t.upcomingReservationInfo) extra = ` • Đặt: ${t.upcomingReservationInfo}`;
                tooltip.innerHTML = `<strong>${t.code}</strong> (${t.zone} • ${t.capacity} chỗ)<br/><span>${t.statusText}${extra}</span>`;
                tooltip.style.left = `${ev.clientX - rect.left}px`;
                tooltip.style.top = `${ev.clientY - rect.top}px`;
                tooltip.style.display = 'block';
            }
        } else {
            renderer.domElement.style.cursor = 'default';
            if (tooltip) tooltip.style.display = 'none';
        }
    });

    renderer.domElement.addEventListener('pointerup', (ev) => {
        if (draggingTableId !== null && editable && dragModeEnabled) {
            const grp = tableGroups.get(draggingTableId);
            const tbl = currentTables.find(x => x.id === draggingTableId);
            if (grp && tbl) {
                tbl.posX = Number(grp.position.x.toFixed(2));
                tbl.posY = Number(grp.position.z.toFixed(2));
                tbl.zone = detectZoneByCoords(tbl.posX, tbl.posY);
                onPositionChanged(tbl);
            }
            draggingTableId = null;
            controls.enabled = true;
            renderer.domElement.style.cursor = 'grab';
            return;
        }

        const movedDist = Math.hypot(ev.clientX - pointerDownPos.x, ev.clientY - pointerDownPos.y);
        if (movedDist > 6) return; // Đang xoay camera, không phải click

        updatePointer(ev);
        raycaster.setFromCamera(pointer, camera);
        const hits = raycaster.intersectObjects(pickableHitboxes, false);
        if (hits.length > 0) {
            const tId = hits[0].object.userData.tableId;
            const tbl = currentTables.find(x => x.id === tId);
            if (!tbl) return;

            if (onlySelectAvailable && tbl.status !== 0) {
                window.DatMocRealtime?.showToast('Bàn không trống', `Bàn ${tbl.code} hiện đang ở trạng thái "${tbl.statusText}". Vui lòng chọn bàn màu xanh lá.`, 'warning');
                return;
            }

            selectedTableId = tbl.id;
            buildTablesScene();
            onSelectTable(tbl);
        }
    });

    let targetCam = null;
    let targetLook = null;

    function animate() {
        requestAnimationFrame(animate);
        if (targetCam && targetLook) {
            camera.position.lerp(targetCam, 0.09);
            controls.target.lerp(targetLook, 0.09);
            if (camera.position.distanceTo(targetCam) < 0.08) {
                targetCam = null;
                targetLook = null;
            }
        }
        controls.update();
        renderer.render(scene, camera);
    }
    animate();

    window.addEventListener('resize', () => {
        if (!container.clientWidth || !container.clientHeight) return;
        camera.aspect = container.clientWidth / container.clientHeight;
        camera.updateProjectionMatrix();
        renderer.setSize(container.clientWidth, container.clientHeight);
    });

    return {
        setTables(newTables) {
            currentTables = Array.isArray(newTables) ? [...newTables] : [];
            buildTablesScene();
        },
        selectTable(tableId) {
            selectedTableId = tableId;
            buildTablesScene();
        },
        setDragMode(enabled) {
            dragModeEnabled = !!enabled;
            gridHelper.visible = dragModeEnabled;
        },
        focusZone(zone) {
            if (zone === 'Tầng 1') {
                targetCam = new THREE.Vector3(-4.2, 10.5, 9.8);
                targetLook = new THREE.Vector3(-4.2, 0, 1.0);
            } else if (zone === 'Tầng 2') {
                targetCam = new THREE.Vector3(4.3, 9.5, 4.8);
                targetLook = new THREE.Vector3(4.3, 0, -2.2);
            } else if (zone === 'Sân vườn') {
                targetCam = new THREE.Vector3(4.3, 9.5, 11.8);
                targetLook = new THREE.Vector3(4.3, 0, 4.7);
            } else {
                targetCam = new THREE.Vector3(0, 14.5, 13.8);
                targetLook = new THREE.Vector3(0, 0, 1.2);
            }
        }
    };
}

