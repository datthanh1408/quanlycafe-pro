/* ==========================================================================
   THREE.JS MODULE 1: KHÔNG GIAN 3D QUÁN ĐẠT MỘC COFFEE (Trang chủ / Giới thiệu)
   Dựng không gian kiến trúc 3D tương tác: Quầy Bar Espresso, Tầng 1, Tầng 2, Sân vườn
   ========================================================================== */
import * as THREE from '../lib/three/three.module.min.js';
import { OrbitControls } from '../lib/three/OrbitControls.js';

export function initCafeHero3D(containerId, tablesData = []) {
    const container = document.getElementById(containerId);
    if (!container) return null;

    const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    const scene = new THREE.Scene();
    scene.background = new THREE.Color(0x1b120d);
    scene.fog = new THREE.FogExp2(0x1b120d, 0.025);

    const camera = new THREE.PerspectiveCamera(45, container.clientWidth / container.clientHeight, 0.1, 100);
    camera.position.set(10.5, 8.2, 12.5);

    const renderer = new THREE.WebGLRenderer({ antialias: true, powerPreference: 'high-performance' });
    renderer.setSize(container.clientWidth, container.clientHeight);
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    renderer.shadowMap.enabled = true;
    renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    container.appendChild(renderer.domElement);

    const controls = new OrbitControls(camera, renderer.domElement);
    controls.enableDamping = true;
    controls.dampingFactor = 0.06;
    controls.maxPolarAngle = Math.PI / 2.08;
    controls.minDistance = 4.5;
    controls.maxDistance = 24;
    controls.target.set(0, 1.0, 0.8);
    controls.autoRotate = !prefersReducedMotion;
    controls.autoRotateSpeed = 0.45;

    // Hệ thống ánh sáng ấm cúng (Ngày / Đêm)
    const ambientLight = new THREE.AmbientLight(0xffe8d1, 0.65);
    scene.add(ambientLight);

    const sunLight = new THREE.DirectionalLight(0xfff1e0, 1.35);
    sunLight.position.set(8, 14, 9);
    sunLight.castShadow = true;
    sunLight.shadow.mapSize.set(1024, 1024);
    scene.add(sunLight);

    const barWarmLight = new THREE.PointLight(0xff9d42, 2.2, 12);
    barWarmLight.position.set(-2.5, 3.1, -4.8);
    scene.add(barWarmLight);

    const gardenLight = new THREE.PointLight(0xffb76b, 1.6, 12);
    gardenLight.position.set(3.8, 2.8, 4.8);
    scene.add(gardenLight);

    // Vật liệu dùng chung
    const matOakFloor = new THREE.MeshStandardMaterial({ color: 0x5c3c24, roughness: 0.72 });
    const matLoftFloor = new THREE.MeshStandardMaterial({ color: 0x734d31, roughness: 0.65 });
    const matGardenFloor = new THREE.MeshStandardMaterial({ color: 0x354a34, roughness: 0.88 });
    const matWall = new THREE.MeshStandardMaterial({ color: 0xf2e6d8, roughness: 0.85 });
    const matDarkWood = new THREE.MeshStandardMaterial({ color: 0x362013, roughness: 0.55 });
    const matMarble = new THREE.MeshStandardMaterial({ color: 0xfaf6f0, roughness: 0.25, metalness: 0.05 });
    const matChrome = new THREE.MeshStandardMaterial({ color: 0xd4d8dd, roughness: 0.2, metalness: 0.85 });
    const matCopper = new THREE.MeshStandardMaterial({ color: 0xc8782a, roughness: 0.3, metalness: 0.7 });

    // 1. Mặt sàn 3 khu vực: Tầng 1, Tầng 2 (gác lửng nâng cao nhẹ), Sân vườn xanh mát
    const floorT1 = new THREE.Mesh(new THREE.BoxGeometry(8.6, 0.3, 12.6), matOakFloor);
    floorT1.position.set(-3.6, -0.15, 0);
    floorT1.receiveShadow = true;
    scene.add(floorT1);

    const floorT2 = new THREE.Mesh(new THREE.BoxGeometry(7.6, 0.55, 7.6), matLoftFloor);
    floorT2.position.set(4.5, -0.02, -2.5);
    floorT2.receiveShadow = true;
    scene.add(floorT2);

    const floorGarden = new THREE.Mesh(new THREE.BoxGeometry(7.6, 0.28, 6.2), matGardenFloor);
    floorGarden.position.set(4.5, -0.16, 4.4);
    floorGarden.receiveShadow = true;
    scene.add(floorGarden);

    // Tường hậu & vách gỗ trang trí thương hiệu
    const backWall = new THREE.Mesh(new THREE.BoxGeometry(16.4, 4.4, 0.3), matWall);
    backWall.position.set(0.4, 2.1, -6.3);
    backWall.receiveShadow = true;
    scene.add(backWall);

    const accentPanel = new THREE.Mesh(new THREE.BoxGeometry(6.5, 2.8, 0.12), matDarkWood);
    accentPanel.position.set(-3.2, 2.3, -6.1);
    scene.add(accentPanel);

    // 2. Quầy Bar Pha Chế Espresso + Tủ bánh ngọt kính
    const barBase = new THREE.Mesh(new THREE.BoxGeometry(5.4, 1.1, 1.25), matDarkWood);
    barBase.position.set(-3.2, 0.55, -4.6);
    barBase.castShadow = true;
    barBase.receiveShadow = true;
    scene.add(barBase);

    const barTop = new THREE.Mesh(new THREE.BoxGeometry(5.6, 0.1, 1.4), matMarble);
    barTop.position.set(-3.2, 1.15, -4.6);
    scene.add(barTop);

    // Máy pha Espresso 2 group
    const espressoBody = new THREE.Mesh(new THREE.BoxGeometry(1.1, 0.62, 0.65), matChrome);
    espressoBody.position.set(-4.1, 1.5, -4.6);
    espressoBody.castShadow = true;
    scene.add(espressoBody);

    const espressoAccent = new THREE.Mesh(new THREE.BoxGeometry(1.14, 0.14, 0.68), matCopper);
    espressoAccent.position.set(-4.1, 1.76, -4.6);
    scene.add(espressoAccent);

    // Tủ kính trưng bày bánh ngọt Croissant & Tiramisu
    const glassMat = new THREE.MeshPhysicalMaterial({
        color: 0xffffff, transparent: true, opacity: 0.34, roughness: 0.1
    });
    const pastryCase = new THREE.Mesh(new THREE.BoxGeometry(1.5, 0.68, 0.8), glassMat);
    pastryCase.position.set(-1.6, 1.54, -4.6);
    scene.add(pastryCase);

    for (let i = 0; i < 3; i++) {
        const pastry = new THREE.Mesh(
            new THREE.CylinderGeometry(0.13, 0.15, 0.12, 16),
            new THREE.MeshStandardMaterial({ color: i === 1 ? 0x5d4037 : 0xd97706, roughness: 0.6 })
        );
        pastry.position.set(-2.0 + i * 0.4, 1.32, -4.6);
        scene.add(pastry);
    }

    // Đèn thả trần ấm áp phía trên quầy bar
    const lampGlowMat = new THREE.MeshBasicMaterial({ color: 0xffd28a });
    [-4.6, -3.2, -1.8].forEach(lx => {
        const cord = new THREE.Mesh(new THREE.CylinderGeometry(0.015, 0.015, 1.3), matDarkWood);
        cord.position.set(lx, 3.65, -4.6);
        scene.add(cord);

        const shade = new THREE.Mesh(new THREE.ConeGeometry(0.28, 0.32, 20), matCopper);
        shade.position.set(lx, 2.95, -4.6);
        scene.add(shade);

        const bulb = new THREE.Mesh(new THREE.SphereGeometry(0.1, 12, 12), lampGlowMat);
        bulb.position.set(lx, 2.8, -4.6);
        scene.add(bulb);
    });

    // 3. Cây xanh trang trí khu Sân vườn
    function addGardenTree(tx, tz, scale = 1) {
        const pot = new THREE.Mesh(
            new THREE.CylinderGeometry(0.32 * scale, 0.24 * scale, 0.48 * scale, 16),
            new THREE.MeshStandardMaterial({ color: 0xb45309, roughness: 0.8 })
        );
        pot.position.set(tx, 0.24 * scale, tz);
        pot.castShadow = true;
        scene.add(pot);

        const trunk = new THREE.Mesh(
            new THREE.CylinderGeometry(0.07 * scale, 0.1 * scale, 0.9 * scale, 10),
            matDarkWood
        );
        trunk.position.set(tx, 0.85 * scale, tz);
        scene.add(trunk);

        const foliageMat = new THREE.MeshStandardMaterial({ color: 0x2e7d32, roughness: 0.75 });
        const crown1 = new THREE.Mesh(new THREE.SphereGeometry(0.58 * scale, 16, 16), foliageMat);
        crown1.position.set(tx, 1.45 * scale, tz);
        crown1.castShadow = true;
        scene.add(crown1);

        const crown2 = new THREE.Mesh(new THREE.SphereGeometry(0.42 * scale, 14, 14), foliageMat);
        crown2.position.set(tx + 0.25 * scale, 1.75 * scale, tz - 0.15 * scale);
        scene.add(crown2);
    }

    addGardenTree(7.4, 2.1, 1.1);
    addGardenTree(7.4, 6.7, 1.25);
    addGardenTree(0.9, 6.8, 0.95);

    // 4. Dựng bàn ghế 3D thực tế từ dữ liệu SQL Server (tablesData)
    const steamParticles = [];
    const statusColors = {
        0: 0x10b981, // Trống
        1: 0xf59e0b, // Đang sử dụng
        2: 0x3b82f6  // Đã đặt trước
    };

    const sampleTables = (tablesData && tablesData.length > 0) ? tablesData : [
        { code: 'T1-01', posX: -5.5, posY: -1.5, capacity: 2, status: 0, zone: 'Tầng 1' },
        { code: 'T1-02', posX: -2.2, posY: -1.5, capacity: 4, status: 1, zone: 'Tầng 1' },
        { code: 'T1-03', posX: -5.5, posY: 2.2, capacity: 4, status: 0, zone: 'Tầng 1' },
        { code: 'T2-01', posX: 2.5, posY: -3.0, capacity: 4, status: 0, zone: 'Tầng 2' },
        { code: 'T2-02', posX: 5.6, posY: -3.0, capacity: 6, status: 2, zone: 'Tầng 2' },
        { code: 'SV-01', posX: 2.6, posY: 4.0, capacity: 4, status: 1, zone: 'Sân vườn' },
        { code: 'SV-02', posX: 5.6, posY: 4.5, capacity: 6, status: 0, zone: 'Sân vườn' }
    ];

    sampleTables.forEach(t => {
        const yBase = t.zone === 'Tầng 2' ? 0.26 : 0.0;
        const group = new THREE.Group();
        group.position.set(t.posX || 0, yBase, (t.posY || 0) * 0.82);

        const radius = (t.capacity || 4) >= 6 ? 0.82 : 0.62;
        const topMesh = new THREE.Mesh(
            new THREE.CylinderGeometry(radius, radius, 0.07, 24),
            matMarble
        );
        topMesh.position.y = 0.76;
        topMesh.castShadow = true;
        group.add(topMesh);

        const legMesh = new THREE.Mesh(
            new THREE.CylinderGeometry(0.08, 0.22, 0.74, 14),
            matDarkWood
        );
        legMesh.position.y = 0.37;
        group.add(legMesh);

        // Vòng sáng trạng thái dưới chân bàn
        const ringColor = statusColors[t.status] ?? 0x10b981;
        const ring = new THREE.Mesh(
            new THREE.RingGeometry(radius * 0.75, radius * 0.95, 28),
            new THREE.MeshBasicMaterial({ color: ringColor, side: THREE.DoubleSide, transparent: true, opacity: 0.8 })
        );
        ring.rotation.x = -Math.PI / 2;
        ring.position.y = 0.02;
        group.add(ring);

        // Ghế xung quanh bàn
        const chairCount = Math.min(Math.max(t.capacity || 4, 2), 6);
        for (let c = 0; c < chairCount; c++) {
            const angle = (c / chairCount) * Math.PI * 2;
            const seat = new THREE.Mesh(new THREE.BoxGeometry(0.36, 0.06, 0.36), matDarkWood);
            seat.position.set(Math.cos(angle) * (radius + 0.34), 0.45, Math.sin(angle) * (radius + 0.34));
            seat.rotation.y = -angle;
            seat.castShadow = true;
            group.add(seat);
        }

        // Nếu bàn Đang sử dụng -> đặt tách cà phê sứ 3D kèm hạt khói bốc lên
        if (t.status === 1) {
            const cup = new THREE.Mesh(
                new THREE.CylinderGeometry(0.11, 0.08, 0.16, 16),
                new THREE.MeshStandardMaterial({ color: 0xfff8f0 })
            );
            cup.position.set(0.12, 0.87, 0.05);
            group.add(cup);

            for (let s = 0; s < 3; s++) {
                const puff = new THREE.Mesh(
                    new THREE.SphereGeometry(0.045, 8, 8),
                    new THREE.MeshBasicMaterial({ color: 0xffffff, transparent: true, opacity: 0.45 })
                );
                puff.position.set(0.12, 0.98 + s * 0.14, 0.05);
                group.add(puff);
                steamParticles.push({ mesh: puff, baseY: 0.96, offset: s * 2.1 });
            }
        }

        scene.add(group);
    });

    // Camera Presets & Chuyển đổi Ngày / Đêm
    let targetCamPos = null;
    let targetLookAt = null;

    const presets = {
        overview: { pos: new THREE.Vector3(10.5, 8.2, 12.5), look: new THREE.Vector3(0, 1.0, 0.8) },
        bar: { pos: new THREE.Vector3(-3.0, 2.7, -0.5), look: new THREE.Vector3(-3.2, 1.4, -4.6) },
        floor1: { pos: new THREE.Vector3(-4.0, 5.2, 6.8), look: new THREE.Vector3(-3.8, 0.6, 0.2) },
        floor2: { pos: new THREE.Vector3(8.5, 5.4, 2.2), look: new THREE.Vector3(4.0, 0.8, -2.2) },
        garden: { pos: new THREE.Vector3(8.2, 4.5, 9.2), look: new THREE.Vector3(4.2, 0.7, 4.2) }
    };

    let isNightMode = false;
    function setLightingMode(night) {
        isNightMode = night;
        if (night) {
            scene.background = new THREE.Color(0x0f0a07);
            scene.fog.color.setHex(0x0f0a07);
            ambientLight.intensity = 0.28;
            sunLight.intensity = 0.25;
            barWarmLight.intensity = 3.6;
            gardenLight.intensity = 2.9;
        } else {
            scene.background = new THREE.Color(0x231710);
            scene.fog.color.setHex(0x231710);
            ambientLight.intensity = 0.72;
            sunLight.intensity = 1.4;
            barWarmLight.intensity = 2.0;
            gardenLight.intensity = 1.5;
        }
    }

    const clock = new THREE.Clock();
    let animId = null;

    function animate() {
        animId = requestAnimationFrame(animate);
        const elapsed = clock.getElapsedTime();

        if (targetCamPos && targetLookAt) {
            camera.position.lerp(targetCamPos, 0.07);
            controls.target.lerp(targetLookAt, 0.07);
            if (camera.position.distanceTo(targetCamPos) < 0.08) {
                targetCamPos = null;
                targetLookAt = null;
            }
        }

        if (!prefersReducedMotion) {
            steamParticles.forEach(p => {
                const cycle = ((elapsed * 0.35 + p.offset) % 1);
                p.mesh.position.y = p.baseY + cycle * 0.55;
                p.mesh.material.opacity = Math.sin(cycle * Math.PI) * 0.5;
            });
        }

        controls.update();
        renderer.render(scene, camera);
    }

    animate();

    const onResize = () => {
        if (!container.clientWidth || !container.clientHeight) return;
        camera.aspect = container.clientWidth / container.clientHeight;
        camera.updateProjectionMatrix();
        renderer.setSize(container.clientWidth, container.clientHeight);
    };
    window.addEventListener('resize', onResize);

    return {
        flyToZone(zoneKey) {
            const p = presets[zoneKey] || presets.overview;
            controls.autoRotate = false;
            targetCamPos = p.pos.clone();
            targetLookAt = p.look.clone();
        },
        toggleNightMode() {
            setLightingMode(!isNightMode);
            return isNightMode;
        }
    };
}

