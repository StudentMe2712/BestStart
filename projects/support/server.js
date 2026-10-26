require('dotenv').config();
const express = require('express');
const bodyParser = require('body-parser');
const session = require('express-session');
const path = require('path');
const fs = require('fs');
const crypto = require('crypto');
const multer = require('multer');
const { marked } = require('marked');
const createDOMPurify = require('dompurify');
const { JSDOM } = require('jsdom');
const XLSX = require('xlsx');
const mammoth = require('mammoth');
const WordExtractor = require('word-extractor');

const USE_STUB = (process.env.DB_STUB === 'true' || process.env.DB_STUB === '1') && fs.existsSync(path.join(__dirname, 'db-stub.js'));

let pool, initDatabase, registerUser, authenticateUser, getUserById, updateProfile, getAllUsers, adminResetPassword, changeUserPassword, adminCreateUser, adminUpdateUser, adminDeleteUser, checkUserExists, resetPassword, initOrganizationTable, organizationsDB, initDatabases1CTable, databases1CDB, initUserPinsTable, userPinsDB, initFilesTable, filesDB;

if (USE_STUB) {
    console.log('⚠️  Using in-memory DB stub (DB_STUB=true)');
    const stub = require('./db-stub');
    pool = stub.pool;
    initDatabase = stub.initDatabase;
    registerUser = stub.registerUser;
    authenticateUser = stub.authenticateUser;
    getUserById = stub.getUserById;
    updateProfile = stub.updateProfile;
    getAllUsers = stub.getAllUsers || (() => []);
    adminResetPassword = stub.adminResetPassword || (() => ({ success: false }));
    changeUserPassword = stub.changeUserPassword || (() => ({ success: false }));
    adminCreateUser = stub.adminCreateUser || (() => ({ success: false }));
    adminUpdateUser = stub.adminUpdateUser || (() => ({ success: false }));
    adminDeleteUser = stub.adminDeleteUser || (() => ({ success: false }));
    checkUserExists = stub.checkUserExists;
    resetPassword = stub.resetPassword;
    initOrganizationTable = stub.initOrganizationTable;
    organizationsDB = stub.organizationsDB;
    initDatabases1CTable = stub.initDatabases1CTable;
    databases1CDB = stub.databases1CDB;
    initUserPinsTable = stub.initUserPinsTable;
    userPinsDB = stub.userPinsDB;
    initFilesTable = stub.initFilesTable;
    filesDB = stub.filesDB;
} else {
    const MySQLStore = require('express-mysql-session')(session);
    const dbModule = require('./database');
    const authModule = require('./auth');
    pool = authModule.pool;
    initDatabase = authModule.initDatabase;
    registerUser = authModule.registerUser;
    authenticateUser = authModule.authenticateUser;
    getUserById = authModule.getUserById;
    updateProfile = authModule.updateProfile;
    getAllUsers = authModule.getAllUsers;
    adminResetPassword = authModule.adminResetPassword;
    changeUserPassword = authModule.changeUserPassword;
    adminCreateUser = authModule.adminCreateUser;
    adminUpdateUser = authModule.adminUpdateUser;
    adminDeleteUser = authModule.adminDeleteUser;
    initOrganizationTable = dbModule.initOrganizationTable;
    organizationsDB = dbModule.organizationsDB;
    initDatabases1CTable = dbModule.initDatabases1CTable;
    databases1CDB = dbModule.databases1CDB;
    initUserPinsTable = dbModule.initUserPinsTable;
    userPinsDB = dbModule.userPinsDB;
    initFilesTable = dbModule.initFilesTable;
    filesDB = dbModule.filesDB;
}

const app = express();
const PORT = process.env.PORT || 3000;

// Required for Plesk/Nginx reverse proxy — makes req.secure and req.ip work correctly
app.set('trust proxy', 1);

// Middleware базовых HTTP-заголовков безопасности (Спринт 7)
app.use((req, res, next) => {
    res.setHeader('X-Content-Type-Options', 'nosniff');
    res.setHeader('X-Frame-Options', 'SAMEORIGIN');
    res.setHeader('Referrer-Policy', 'same-origin');
    next();
});

// Инициализация базы данных для пользователей
initDatabase().catch(err => {
    console.error('Не удалось инициализировать базу данных пользователей:', err);
});

// Инициализация таблицы организаций
initOrganizationTable().catch(err => {
    console.error('Не удалось инициализировать таблицу организаций:', err);
});

// Инициализация таблицы баз 1С
initDatabases1CTable().catch(err => {
    console.error('Не удалось инициализировать таблицу баз 1С:', err);
});

// Инициализация таблицы закреплений организаций (Спринт 7)
initUserPinsTable().catch(err => {
    console.error('Не удалось инициализировать таблицу user_pins:', err);
});

// Инициализация таблицы прикрепленных файлов (Спринт 7)
initFilesTable().catch(err => {
    console.error('Не удалось инициализировать таблицу organization_files:', err);
});

// Настройка DOMPurify для безопасности
const window = new JSDOM('').window;
const DOMPurify = createDOMPurify(window);

// Middleware
app.use(bodyParser.json());
app.use(bodyParser.urlencoded({ extended: true }));
app.use(express.static(path.join(__dirname, 'public')));
app.set('view engine', 'ejs');
app.set('views', path.join(__dirname, 'views'));

// Настройка сессий
let sessionStore;
if (USE_STUB) {
    sessionStore = new session.MemoryStore();
} else {
    const MySQLStore = require('express-mysql-session')(session);
    sessionStore = new MySQLStore({
        host: process.env.DB_HOST || 'localhost',
        port: process.env.DB_PORT || 3306,
        user: process.env.DB_USER || 'p-14802_adminits',
        password: process.env.DB_PASSWORD || 'ASDFzxcv@113525',
        database: process.env.DB_NAME || 'p-14802_wiki',
        clearExpired: true,
        checkExpirationInterval: 900000,
        expiration: 86400000,
        endConnectionOnClose: true,
    }, pool);
}

app.use(session({
    secret: process.env.SESSION_SECRET || 'its-kom-wiki-secret-key-2025-secure',
    store: sessionStore,
    resave: false,
    saveUninitialized: false,
    cookie: {
        maxAge: 30 * 24 * 60 * 60 * 1000,
        httpOnly: true,
        secure: false,   // set to true only if Node.js receives HTTPS directly (no proxy)
        sameSite: 'lax'
    }
}));

// Определение типа запроса: API/JSON или стандартный браузерный HTML переход
function isApiRequest(req) {
    return req.path.startsWith('/api/') ||
           req.xhr ||
           req.is('json') ||
           Boolean(req.headers.accept && req.headers.accept.includes('application/json'));
}

// Middleware для проверки авторизации (Спринт 12)
// Проверяет наличие сессии, валидирует token_version в БД и контролирует обязательную смену пароля
async function requireAuth(req, res, next) {
    const sessionUserId = (req.session && req.session.user && req.session.user.id) || (req.session && req.session.userId);
    if (!sessionUserId) {
        if (isApiRequest(req)) {
            return res.status(401).json({ success: false, error: 'Требуется авторизация' });
        }
        return res.redirect('/login');
    }

    try {
        const user = await getUserById(sessionUserId);
        if (!user) {
            req.session.destroy(() => {});
            if (isApiRequest(req)) {
                return res.status(401).json({ success: false, error: 'Пользователь не найден' });
            }
            return res.redirect('/login');
        }

        // Проверка инвалидации сессии: при сбросе/смене пароля token_version увеличивается
        const sessionTokenVersion = req.session.token_version || (req.session.user && req.session.user.tokenVersion);
        if (sessionTokenVersion && user.token_version !== sessionTokenVersion) {
            req.session.destroy(() => {});
            if (isApiRequest(req)) {
                return res.status(401).json({ success: false, error: 'Сессия устарела. Войдите снова.' });
            }
            return res.redirect('/login?expired=1');
        }

        // Актуализируем объект пользователя в сессии из доверенной БД
        if (!req.session.user) {
            req.session.user = { id: user.id, username: user.username };
        }
        req.session.user.role = user.role || 'engineer';
        req.session.user.must_change_password = Boolean(user.must_change_password);
        req.session.user.tokenVersion = user.token_version || 1;
        req.session.token_version = user.token_version || 1;

        next();
    } catch (err) {
        console.error('Ошибка в requireAuth:', err.message);
        if (isApiRequest(req)) {
            return res.status(500).json({ success: false, error: 'Ошибка проверки авторизации' });
        }
        return res.redirect('/login');
    }
}

// Middleware для проверки прав администратора (Спринт 12)
// Проверяет актуальную роль пользователя напрямую в БД (никаких клиентских подмен роли)
async function requireAdmin(req, res, next) {
    const sessionUserId = (req.session && req.session.user && req.session.user.id) || (req.session && req.session.userId);
    if (!sessionUserId) {
        if (isApiRequest(req)) {
            return res.status(401).json({ success: false, error: 'Требуется авторизация' });
        }
        return res.redirect('/login');
    }

    try {
        const user = await getUserById(sessionUserId);
        if (!user || user.role !== 'admin') {
            if (isApiRequest(req)) {
                return res.status(403).json({ success: false, error: 'Доступ запрещен: требуются права администратора' });
            }
            return res.status(403).render('login', {
                error: 'Доступ запрещен: требуются права администратора',
                isStubMode: USE_STUB
            });
        }
        next();
    } catch (err) {
        console.error('Ошибка в requireAdmin:', err.message);
        if (isApiRequest(req)) {
            return res.status(500).json({ success: false, error: 'Ошибка проверки прав' });
        }
        return res.status(500).send('Ошибка проверки прав');
    }
}

// ============================================================
// НАСТРОЙКА MULTER ДЛЯ ЗАГРУЗКИ ФАЙЛОВ ОРГАНИЗАЦИЙ (Спринт 7)
// ============================================================
function fixUtf8Filename(name) {
    if (!name || typeof name !== 'string') return name;
    if (/[^\u0000-\u00ff]/.test(name)) return name;
    try {
        const decoded = Buffer.from(name, 'latin1').toString('utf8');
        if (decoded && !decoded.includes('\uFFFD')) {
            return decoded;
        }
    } catch (e) {}
    return name;
}

const ALLOWED_EXTENSIONS = [
    '.xlsx', '.xls', '.csv', '.pdf', '.docx', '.doc',
    '.png', '.jpg', '.jpeg', '.webp', '.txt', '.zip', '.rdp',
    '.xml'
];

const storage = multer.diskStorage({
    destination: (req, file, cb) => {
        const orgId = req.params.id;
        const uploadDir = path.join(__dirname, 'uploads', `org_${orgId}`);
        if (!fs.existsSync(uploadDir)) {
            fs.mkdirSync(uploadDir, { recursive: true });
        }
        cb(null, uploadDir);
    },
    filename: (req, file, cb) => {
        const uniqueSuffix = `${Date.now()}-${Math.round(Math.random() * 1e9)}`;
        const cleanName = fixUtf8Filename(file.originalname);
        const ext = path.extname(cleanName || file.originalname).toLowerCase();
        cb(null, `${uniqueSuffix}${ext}`);
    }
});

const fileFilter = (req, file, cb) => {
    const ext = path.extname(file.originalname || '').toLowerCase();
    const mime = (file.mimetype || '').toLowerCase();
    const allowedMimes = [
        'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
        'application/msword',
        'application/xml',
        'text/xml'
    ];
    if (ALLOWED_EXTENSIONS.includes(ext) || allowedMimes.includes(mime) || ext === '.xml') {
        cb(null, true);
    } else {
        cb(new Error(`Недопустимый тип файла (${ext}). Разрешены только: ${ALLOWED_EXTENSIONS.join(', ')}`));
    }
};

const upload = multer({
    storage,
    limits: { fileSize: 25 * 1024 * 1024 }, // 25 МБ
    fileFilter
});

const uploadSingleFile = (req, res, next) => {
    upload.single('file')(req, res, (err) => {
        if (err instanceof multer.MulterError) {
            if (err.code === 'LIMIT_FILE_SIZE') {
                return res.status(400).json({ success: false, error: 'Размер файла превышает допустимый лимит (25 МБ)' });
            }
            return res.status(400).json({ success: false, error: `Ошибка загрузки файла: ${err.message}` });
        } else if (err) {
            return res.status(400).json({ success: false, error: err.message });
        }
        next();
    });
};

// ============================================================
// ВСТРОЕННЫЙ IN-MEMORY RATE LIMITER (Спринт 7 & Спринт 12)
// Ограничение: максимум 5 неудачных попыток за 15 минут с одного IP.
// Защищаемые маршруты:
// - POST /login
// - POST /api/admin/users/:id/reset-password
// - PUT /api/profile/password
// ============================================================
const RATE_LIMIT_WINDOW_MS = 15 * 60 * 1000; // 15 минут
const RATE_LIMIT_MAX_FAILURES = 5;
const failedAttemptsMap = new Map(); // ip -> [timestamp1, timestamp2, ...]

function getClientIp(req) {
    let ip = req.ip || req.connection?.remoteAddress || req.socket?.remoteAddress || '127.0.0.1';
    if (ip === '::1' || ip === '::ffff:127.0.0.1') {
        ip = '127.0.0.1';
    }
    return ip;
}

function getRecentFailedAttempts(ip) {
    const now = Date.now();
    const attempts = failedAttemptsMap.get(ip) || [];
    const validAttempts = attempts.filter(ts => (now - ts) < RATE_LIMIT_WINDOW_MS);
    if (validAttempts.length > 0) {
        failedAttemptsMap.set(ip, validAttempts);
    } else {
        failedAttemptsMap.delete(ip);
    }
    return validAttempts;
}

function isRateLimited(ip) {
    const attempts = getRecentFailedAttempts(ip);
    return attempts.length >= RATE_LIMIT_MAX_FAILURES;
}

function recordFailedAttempt(ip) {
    const attempts = getRecentFailedAttempts(ip);
    attempts.push(Date.now());
    failedAttemptsMap.set(ip, attempts);
}

function clearRateLimit(ip) {
    failedAttemptsMap.delete(ip);
}

// Middleware проверки Rate Limit
function checkAuthRateLimit(req, res, next) {
    const ip = getClientIp(req);
    if (isRateLimited(ip)) {
        const message = 'Слишком много неудачных попыток. Пожалуйста, повторите попытку через 15 минут.';
        if (req.xhr || req.is('json') || req.headers.accept?.includes('application/json') || req.path.startsWith('/api/')) {
            return res.status(429).json({ success: false, error: message });
        }
        return res.status(429).render('login', {
            error: message,
            isStubMode: USE_STUB
        });
    }
    next();
}

const rateLimitCleanupTimer = setInterval(() => {
    const now = Date.now();
    for (const [ip, timestamps] of failedAttemptsMap.entries()) {
        const valid = timestamps.filter(ts => (now - ts) < RATE_LIMIT_WINDOW_MS);
        if (valid.length === 0) {
            failedAttemptsMap.delete(ip);
        } else {
            failedAttemptsMap.set(ip, valid);
        }
    }
}, 5 * 60 * 1000);
if (rateLimitCleanupTimer.unref) {
    rateLimitCleanupTimer.unref();
}

// Страница логина
app.get('/login', (req, res) => {
    if (req.session && req.session.user) {
        return res.redirect('/');
    }
    res.render('login', { error: null, isStubMode: USE_STUB });
});

// Обработка логина с защитой Rate Limiter
app.post('/login', checkAuthRateLimit, async (req, res) => {
    const { username, password, remember } = req.body;
    const clientIp = getClientIp(req);
    
    try {
        const result = await authenticateUser(username, password);
        
        if (result.success) {
            clearRateLimit(clientIp);
            req.session.userId = result.user.id;
            req.session.token_version = result.user.token_version || 1;
            req.session.user = {
                id: result.user.id,
                username: result.user.username,
                role: result.user.role || 'engineer',
                must_change_password: result.user.must_change_password,
                tokenVersion: result.user.token_version || 1,
                loginTime: new Date()
            };

            if (remember) {
                req.session.cookie.maxAge = 90 * 24 * 60 * 60 * 1000;
            } else {
                req.session.cookie.maxAge = 24 * 60 * 60 * 1000;
            }

            req.session.save((err) => {
                if (err) {
                    console.error('Ошибка сохранения сессии:', err);
                    if (req.is('json') || req.headers.accept?.includes('application/json')) {
                        return res.status(500).json({ success: false, error: 'Ошибка сессии. Попробуйте ещё раз.' });
                    }
                    return res.render('login', { error: 'Ошибка сессии. Попробуйте ещё раз.', isStubMode: USE_STUB });
                }
                const redirectTarget = '/';
                if (req.is('json') || req.headers.accept?.includes('application/json')) {
                    return res.json({ success: true, redirect: redirectTarget });
                }
                res.redirect(redirectTarget);
            });
        } else {
            recordFailedAttempt(clientIp);
            if (req.is('json') || req.headers.accept?.includes('application/json')) {
                return res.status(401).json({ 
                    success: false, 
                    error: result.error || 'Ошибка входа' 
                });
            }
            res.render('login', { 
                error: result.error || 'Ошибка входа',
                isStubMode: USE_STUB 
            });
        }
    } catch (err) {
        console.error('Ошибка при входе:', err);
        recordFailedAttempt(clientIp);
        if (req.is('json') || req.headers.accept?.includes('application/json')) {
            return res.status(500).json({ 
                success: false, 
                error: 'Ошибка сервера. Попробуйте позже.' 
            });
        }
        res.render('login', { 
            error: 'Ошибка сервера. Попробуйте позже.',
            isStubMode: USE_STUB 
        });
    }
});

// ============================================================
// ПУБЛИЧНАЯ РЕГИСТРАЦИЯ ЗАКРЫТА
// Публичная регистрация отключена в целях корпоративной безопасности.
// Доступ предоставляется только системным администратором.
// ============================================================
app.get('/register', (req, res) => {
    res.redirect('/login');
});

app.post('/register', (req, res) => {
    res.redirect('/login');
});

// ============================================================
// АДМИНИСТРАТИВНОЕ УПРАВЛЕНИЕ ПОЛЬЗОВАТЕЛЯМИ (Спринт 12)
// Доступ разрешён исключительно авторизованным пользователям с ролью 'admin'
// Все публичные API проверки/сброса пароля (verify-login, reset-password) полностью удалены!
// ============================================================

// Страница управления пользователями (HTML)
app.get('/admin/users', requireAuth, requireAdmin, async (req, res) => {
    try {
        const users = await getAllUsers();
        res.render('admin-users', {
            user: req.session.user,
            users: users || [],
            currentRoute: '/admin/users'
        });
    } catch (err) {
        console.error('Ошибка рендеринга страницы пользователей:', err.message);
        res.status(500).send('Ошибка сервера при загрузке страницы пользователей');
    }
});

// Безопасный список пользователей (JSON API)
// password_hash и сессионные секреты исключены из выборки
app.get('/api/admin/users', requireAuth, requireAdmin, async (req, res) => {
    try {
        const users = await getAllUsers();
        res.json({ success: true, users: users || [] });
    } catch (err) {
        console.error('Ошибка получения списка пользователей:', err.message);
        res.status(500).json({ success: false, error: 'Ошибка сервера' });
    }
});

// Административный сброс пароля пользователя с генерацией криптостойкого временного пароля
app.post('/api/admin/users/:id/reset-password', requireAuth, requireAdmin, checkAuthRateLimit, async (req, res) => {
    const targetId = parseInt(req.params.id, 10);
    const adminUser = req.session.user;

    if (isNaN(targetId) || targetId <= 0) {
        return res.status(400).json({ success: false, error: 'Некорректный ID пользователя' });
    }

    if (targetId === adminUser.id) {
        return res.status(400).json({
            success: false,
            error: 'Сброс собственного пароля через административную панель запрещён. Используйте страницу профиля.'
        });
    }

    try {
        const targetUser = await getUserById(targetId);
        if (!targetUser) {
            return res.status(404).json({ success: false, error: 'Пользователь не найден' });
        }

        if (targetUser.role === 'admin') {
            return res.status(403).json({
                success: false,
                error: 'Сброс пароля учётной записи администратора запрещён'
            });
        }

        // Генерация криптографически стойкого временного пароля (12 символов: верхний/нижний регистр, цифры, символы)
        const charset = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%';
        const randomBytes = crypto.randomBytes(12);
        let tempPassword = '';
        for (let i = 0; i < 12; i++) {
            tempPassword += charset[randomBytes[i] % charset.length];
        }

        const resetResult = await adminResetPassword(targetId, tempPassword);
        if (!resetResult.success) {
            console.warn(`[AUDIT] ОШИБКА сброса: админ ID=${adminUser.id} для пользователя ID=${targetId} - ${resetResult.error}`);
            return res.status(500).json({ success: false, error: resetResult.error || 'Ошибка сброса пароля' });
        }

        // Безопасный аудит: фиксируем только ID и имена без паролей и хешей
        console.log(`[AUDIT] Администратор ${adminUser.username} (ID: ${adminUser.id}) выполнил сброс пароля для ${targetUser.username} (ID: ${targetId}) в ${new Date().toISOString()}`);

        return res.json({
            success: true,
            username: targetUser.username,
            newPassword: tempPassword,
            tempPassword: tempPassword // для обратной совместимости со старым кодом
        });
    } catch (err) {
        console.error('Ошибка административного сброса пароля:', err.message);
        return res.status(500).json({ success: false, error: 'Ошибка сервера при сбросе пароля' });
    }
});

// Административное создание нового пользователя (Спринт 12)
app.post('/api/admin/users', requireAuth, requireAdmin, checkAuthRateLimit, async (req, res) => {
    const adminUser = req.session.user;
    const { username, email } = req.body || {};

    if (!username || typeof username !== 'string') {
        return res.status(400).json({
            success: false,
            error: 'Логин должен содержать от 3 до 50 символов (буквы, цифры, дефис, точка, подчеркивание)'
        });
    }

    const trimmedUsername = username.trim();
    const USERNAME_REGEX = /^[a-zA-Z0-9_\-\.]+$/;

    if (trimmedUsername.length < 3 || trimmedUsername.length > 50 || !USERNAME_REGEX.test(trimmedUsername)) {
        return res.status(400).json({
            success: false,
            error: 'Логин должен содержать от 3 до 50 символов (буквы, цифры, дефис, точка, подчеркивание)'
        });
    }

    let cleanEmail = null;
    if (email !== undefined && email !== null && typeof email === 'string' && email.trim().length > 0) {
        cleanEmail = email.trim();
        const EMAIL_REGEX = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
        if (cleanEmail.length > 255 || !EMAIL_REGEX.test(cleanEmail)) {
            return res.status(400).json({
                success: false,
                error: 'Неверный формат электронной почты'
            });
        }
    } else if (email !== undefined && email !== null && typeof email === 'string' && email.trim().length === 0) {
        cleanEmail = null;
    } else if (email !== undefined && email !== null && typeof email !== 'string') {
        return res.status(400).json({
            success: false,
            error: 'Неверный формат электронной почты'
        });
    }

    try {
        // Генерация криптографически стойкого 12-значного пароля
        const charset = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%';
        const randomBytes = crypto.randomBytes(12);
        let generatedPassword = '';
        for (let i = 0; i < 12; i++) {
            generatedPassword += charset[randomBytes[i] % charset.length];
        }

        const result = await adminCreateUser(trimmedUsername, cleanEmail, generatedPassword);
        if (!result.success) {
            const statusCode = result.code === 'USER_EXISTS' ? 409 : 400;
            return res.status(statusCode).json({
                success: false,
                error: result.error
            });
        }

        // Безопасный аудит: фиксируем факт создания пользователя администратором без пароля
        console.log(`[AUDIT] Администратор ${adminUser.username} (ID: ${adminUser.id}) создал пользователя ${result.user.username} (ID: ${result.user.id}) в ${new Date().toISOString()}`);

        return res.status(201).json({
            success: true,
            message: 'Пользователь успешно создан',
            user: result.user,
            newPassword: generatedPassword,
            tempPassword: generatedPassword // для совместимости
        });
    } catch (err) {
        console.error('Ошибка создания пользователя администратором:', err.message);
        return res.status(500).json({
            success: false,
            error: 'Ошибка сервера при создании пользователя'
        });
    }
});

// Административное редактирование данных пользователя (Спринт 13)
app.put('/api/admin/users/:id', requireAuth, requireAdmin, checkAuthRateLimit, async (req, res) => {
    const adminUser = req.session.user;
    const targetId = parseInt(req.params.id, 10);

    if (isNaN(targetId) || targetId <= 0) {
        return res.status(400).json({ success: false, error: 'Некорректный ID пользователя' });
    }

    const { username, email } = req.body || {};

    if (!username || typeof username !== 'string') {
        return res.status(400).json({
            success: false,
            error: 'Логин должен содержать от 3 до 50 символов (буквы, цифры, дефис, точка, подчеркивание)'
        });
    }

    const trimmedUsername = username.trim();
    const USERNAME_REGEX = /^[a-zA-Z0-9_\-\.]+$/;

    if (trimmedUsername.length < 3 || trimmedUsername.length > 50 || !USERNAME_REGEX.test(trimmedUsername)) {
        return res.status(400).json({
            success: false,
            error: 'Логин должен содержать от 3 до 50 символов (буквы, цифры, дефис, точка, подчеркивание)'
        });
    }

    let cleanEmail = null;
    if (email !== undefined && email !== null && typeof email === 'string' && email.trim().length > 0) {
        cleanEmail = email.trim();
        const EMAIL_REGEX = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
        if (cleanEmail.length > 255 || !EMAIL_REGEX.test(cleanEmail)) {
            return res.status(400).json({
                success: false,
                error: 'Неверный формат электронной почты'
            });
        }
    } else if (email !== undefined && email !== null && typeof email === 'string' && email.trim().length === 0) {
        cleanEmail = null;
    } else if (email !== undefined && email !== null && typeof email !== 'string') {
        return res.status(400).json({
            success: false,
            error: 'Неверный формат электронной почты'
        });
    }

    try {
        const result = await adminUpdateUser(targetId, { username: trimmedUsername, email: cleanEmail });
        if (!result.success) {
            const statusCode = result.code === 'USER_EXISTS' ? 409 : 400;
            return res.status(statusCode).json({
                success: false,
                error: result.error
            });
        }

        // Безопасный аудит: фиксируем факт обновления данных пользователя администратором
        console.log(`[AUDIT] Администратор ${adminUser.username} (ID: ${adminUser.id}) обновил данные пользователя ${result.user.username} (ID: ${targetId}) в ${new Date().toISOString()}`);

        // Если текущий администратор обновил свой логин: обновим имя пользователя в активной сессии
        if (targetId === adminUser.id) {
            req.session.user.username = result.user.username;
        }

        return res.status(200).json({
            success: true,
            message: 'Данные пользователя успешно обновлены',
            user: result.user
        });
    } catch (err) {
        console.error('Ошибка редактирования пользователя администратором:', err.message);
        return res.status(500).json({
            success: false,
            error: 'Ошибка сервера при обновлении пользователя'
        });
    }
});

// Административное удаление пользователя (Спринт 13)
app.delete('/api/admin/users/:id', requireAuth, requireAdmin, checkAuthRateLimit, async (req, res) => {
    const adminUser = req.session.user;
    const targetId = parseInt(req.params.id, 10);

    if (isNaN(targetId) || targetId <= 0) {
        return res.status(400).json({ success: false, error: 'Некорректный ID пользователя' });
    }

    // Проверка самоликвидации: удаление собственной учётной записи запрещено
    if (targetId === adminUser.id) {
        return res.status(400).json({
            success: false,
            error: 'Удаление собственной учётной записи запрещено'
        });
    }

    try {
        const result = await adminDeleteUser(targetId, adminUser.id);
        if (!result.success) {
            return res.status(400).json({
                success: false,
                error: result.error
            });
        }

        // Безопасный аудит: фиксируем факт удаления пользователя администратором
        console.log(`[AUDIT] Администратор ${adminUser.username} (ID: ${adminUser.id}) удалил пользователя ${result.user.username} (ID: ${targetId}) в ${new Date().toISOString()}`);

        return res.status(200).json({
            success: true,
            message: 'Пользователь успешно удален'
        });
    } catch (err) {
        console.error('Ошибка удаления пользователя администратором:', err.message);
        return res.status(500).json({
            success: false,
            error: 'Ошибка сервера при удалении пользователя'
        });
    }
});

// Выход
app.get('/logout', (req, res) => {
    req.session.destroy((err) => {
        if (err) {
            console.error('Ошибка при выходе:', err);
        }
        res.redirect('/login');
    });
});

// Профиль пользователя
app.get('/profile', requireAuth, async (req, res) => {
    try {
        const userDetails = await getUserById(req.session.user.id);
        res.render('profile', { user: req.session.user, userDetails });
    } catch (err) {
        console.error('Ошибка профиля:', err);
        res.render('profile', { user: req.session.user, userDetails: null });
    }
});

// Обновление профиля (email)
app.put('/api/profile', requireAuth, async (req, res) => {
    const { email } = req.body;

    if (email && email.trim()) {
        const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
        if (!emailRegex.test(email.trim())) {
            return res.status(400).json({ error: 'Неверный формат email' });
        }
    }

    try {
        const result = await updateProfile(req.session.user.id, {
            email: email && email.trim() ? email.trim() : null
        });
        if (!result.success) {
            return res.status(500).json({ error: result.error || 'Ошибка обновления' });
        }
        res.json({ success: true });
    } catch (err) {
        console.error('Ошибка обновления профиля:', err);
        res.status(500).json({ error: 'Ошибка сервера' });
    }
});

// Главная страница (защищенная)
app.get(['/', '/home'], requireAuth, async (req, res) => {
    try {
        const organizations = await organizationsDB.getAll();
        const serverCount = Array.isArray(organizations) ? organizations.length : 25;
        const allDatabases = await databases1CDB.getAll();
        const totalDatabasesCount = allDatabases ? allDatabases.length : 0;
        const userId = (req.session.user && req.session.user.id) || req.session.userId;
        const userPins = await userPinsDB.getUserPins(userId);
        const pinnedOrgs = Array.isArray(organizations) ? organizations.filter(o => (userPins || []).includes(o.id)) : [];
        const now = new Date();
        const formattedDate = now.toLocaleDateString('ru-RU', {
            weekday: 'long',
            day: 'numeric',
            month: 'long',
            year: 'numeric'
        });
        const currentDateStr = formattedDate.charAt(0).toUpperCase() + formattedDate.slice(1);

        res.render('home', { 
            user: req.session.user,
            serverCount,
            totalDatabasesCount,
            currentDateStr,
            userPins: userPins || [],
            pinnedOrgs: pinnedOrgs || [],
            organizations: organizations || []
        });
    } catch (err) {
        console.error('Ошибка рендеринга главной страницы:', err);
        res.render('home', { 
            user: req.session.user,
            serverCount: 25,
            totalDatabasesCount: 4,
            currentDateStr: 'Четверг, 22 октября 2026 г.',
            userPins: [],
            pinnedOrgs: [],
            organizations: []
        });
    }
});

// Вспомогательная функция формирования монограммы организации (Спринт 6)
function getOrgMonogram(name) {
    if (!name || typeof name !== 'string') return '';
    const clean = name.trim();
    if (!clean) return '';
    if (/^астанагенплан/i.test(clean)) return 'АП';

    const words = clean.split(/[\s\-_–—/\\()]+/).filter(w => /[a-zA-Zа-яА-ЯёЁ]/.test(w));
    if (words.length >= 2) {
        const m1 = words[0].match(/[a-zA-Zа-яА-ЯёЁ]/);
        const m2 = words[1].match(/[a-zA-Zа-яА-ЯёЁ]/);
        if (m1 && m2) return (m1[0] + m2[0]).toUpperCase();
    }
    const letters = clean.replace(/[^a-zA-Zа-яА-ЯёЁ]/g, '');
    if (letters.length >= 2) {
        return letters.substring(0, 2).toUpperCase();
    }
    return letters.toUpperCase();
}

// Страница Wiki (список организаций)
app.get('/wiki', requireAuth, async (req, res) => {
    try {
        const organizations = await organizationsDB.getAll();
        const userId = (req.session.user && req.session.user.id) || req.session.userId;
        const userPins = await userPinsDB.getUserPins(userId);
        res.render('wiki', { 
            organizations, 
            currentOrg: null,
            databases: [],
            user: req.session.user,
            orgMonogram: '',
            userPins: userPins || [],
            files: []
        });
    } catch (err) {
        console.error('Ошибка получения организаций:', err.message);
        return res.status(500).send('Ошибка сервера');
    }
});

// Страница организации (защищенная)
app.get('/wiki/organization/:id', requireAuth, async (req, res) => {
    const orgId = req.params.id;
    
    try {
        const organizations = await organizationsDB.getAll();
        const organization = await organizationsDB.getById(orgId);
        
        if (!organization) {
            return res.status(404).send('Организация не найдена');
        }

        const databases = await databases1CDB.getByOrgId(orgId);
        const userId = (req.session.user && req.session.user.id) || req.session.userId;
        const userPins = await userPinsDB.getUserPins(userId);
        const files = await filesDB.getByOrgId(orgId);
        
        res.render('wiki', { 
            organizations, 
            currentOrg: organization,
            databases: databases || [],
            user: req.session.user,
            orgMonogram: getOrgMonogram(organization.name),
            userPins: userPins || [],
            files: files || []
        });
    } catch (err) {
        console.error('Ошибка получения организации:', err.message);
        return res.status(500).send('Ошибка сервера');
    }
});

// API: Получить все организации
app.get('/api/organizations', requireAuth, async (req, res) => {
    try {
        const organizations = await organizationsDB.getAll();
        res.json(organizations);
    } catch (err) {
        console.error('Ошибка получения организаций:', err.message);
        return res.status(500).json({ error: 'Ошибка сервера' });
    }
});

// API: Получить организацию по ID
app.get('/api/organizations/:id', requireAuth, async (req, res) => {
    try {
        const organization = await organizationsDB.getById(req.params.id);
        
        if (!organization) {
            return res.status(404).json({ error: 'Организация не найдена' });
        }
        
        res.json(organization);
    } catch (err) {
        console.error('Ошибка получения организации:', err.message);
        return res.status(500).json({ error: 'Ошибка сервера' });
    }
});

// API: Создать новую организацию
app.post('/api/organizations', requireAuth, async (req, res) => {
    const { name, content } = req.body;
    
    if (!name || name.trim() === '') {
        return res.status(400).json({ error: 'Название организации обязательно' });
    }
    
    try {
        const insertId = await organizationsDB.create(name.trim(), content || '');
        res.json({ 
            id: insertId, 
            name: name.trim(), 
            content: content || '',
            message: 'Организация успешно создана' 
        });
    } catch (err) {
        console.error('Ошибка создания организации:', err.message);
        if (err.message.includes('Duplicate entry')) { // MariaDB specific error for unique constraint
            return res.status(400).json({ error: 'Организация с таким названием уже существует' });
        }
        return res.status(500).json({ error: 'Ошибка сервера' });
    }
});

// API: Обновить организацию
app.put('/api/organizations/:id', requireAuth, async (req, res) => {
    const { name, content } = req.body;
    const id = req.params.id;
    
    if (!name || name.trim() === '') {
        return res.status(400).json({ error: 'Название организации обязательно' });
    }
    
    try {
        const affectedRows = await organizationsDB.update(id, name.trim(), content || '');
        if (affectedRows === 0) {
            return res.status(404).json({ error: 'Организация не найдена' });
        }
        res.json({ message: 'Организация успешно обновлена' });
    } catch (err) {
        console.error('Ошибка обновления организации:', err.message);
        if (err.message.includes('Duplicate entry')) { // MariaDB specific error for unique constraint
            return res.status(400).json({ error: 'Организация с таким названием уже существует' });
        }
        return res.status(500).json({ error: 'Ошибка сервера' });
    }
});

// API: Удалить организацию
app.delete('/api/organizations/:id', requireAuth, async (req, res) => {
    try {
        const affectedRows = await organizationsDB.delete(req.params.id);
        if (affectedRows === 0) {
            return res.status(404).json({ error: 'Организация не найдена' });
        }
        res.json({ message: 'Организация успешно удалена' });
    } catch (err) {
        console.error('Ошибка удаления организации:', err.message);
        return res.status(500).json({ error: 'Ошибка сервера' });
    }
});

// ============================================================
// API: ЗАКРЕПЛЕНИЕ ОРГАНИЗАЦИЙ (USER PINS — СПРИНТ 7)
// ============================================================

// Переключить закрепление организации
app.post(['/api/organizations/:id/pin', '/api/pins/toggle/:id'], requireAuth, async (req, res) => {
    try {
        const orgId = Number(req.params.id);
        const userId = (req.session.user && req.session.user.id) || req.session.userId;
        const result = await userPinsDB.toggleUserPin(userId, orgId);
        res.json({
            success: true,
            pinned: result.pinned,
            pins: result.pins
        });
    } catch (err) {
        console.error('Ошибка переключения закрепления организации:', err);
        res.status(500).json({ success: false, error: 'Ошибка сервера' });
    }
});

// Получить список ID закрепленных организаций текущего пользователя
app.get(['/api/user/pins', '/api/pins'], requireAuth, async (req, res) => {
    try {
        const userId = (req.session.user && req.session.user.id) || req.session.userId;
        const pins = await userPinsDB.getUserPins(userId);
        res.json({
            success: true,
            pins: pins || []
        });
    } catch (err) {
        console.error('Ошибка получения списка закреплений пользователя:', err);
        res.status(500).json({ success: false, error: 'Ошибка сервера' });
    }
});

// ============================================================
// API: ПРИКРЕПЛЕННЫЕ ФАЙЛЫ (ORGANIZATION FILES — СПРИНТ 7)
// ============================================================

// Загрузить файл для организации
app.post('/api/organizations/:id/files', requireAuth, uploadSingleFile, async (req, res) => {
    try {
        if (!req.file) {
            return res.status(400).json({ success: false, error: 'Файл не прикреплен' });
        }
        const orgId = Number(req.params.id);
        const cleanOriginalName = fixUtf8Filename(req.file.originalname);
        const ext = path.extname(cleanOriginalName || '').toLowerCase();
        let mimeType = req.file.mimetype || 'application/octet-stream';
        if (ext === '.xml' && (!req.file.mimetype || req.file.mimetype === 'application/octet-stream')) {
            mimeType = 'application/xml';
        }
        if (ext === '.docx' && (!req.file.mimetype || req.file.mimetype === 'application/octet-stream')) {
            mimeType = 'application/vnd.openxmlformats-officedocument.wordprocessingml.document';
        }
        if (ext === '.doc' && (!req.file.mimetype || req.file.mimetype === 'application/octet-stream')) {
            mimeType = 'application/msword';
        }
        const createdFile = await filesDB.create({
            org_id: orgId,
            filename: req.file.filename,
            original_name: cleanOriginalName,
            mime_type: mimeType,
            file_size: req.file.size,
            uploaded_by: req.session.user.username
        });
        res.json({
            success: true,
            file: createdFile
        });
    } catch (err) {
        console.error('Ошибка загрузки файла организации:', err);
        res.status(500).json({ success: false, error: 'Ошибка сервера при загрузке файла' });
    }
});

// Получить список файлов организации
app.get('/api/organizations/:id/files', requireAuth, async (req, res) => {
    try {
        const orgId = Number(req.params.id);
        const files = await filesDB.getByOrgId(orgId);
        res.json({
            success: true,
            files: files || []
        });
    } catch (err) {
        console.error('Ошибка получения списка файлов организации:', err);
        res.status(500).json({ success: false, error: 'Ошибка сервера' });
    }
});

// Просмотреть файл (инлайн / lightbox)
app.get('/api/files/:fileId/view', requireAuth, async (req, res) => {
    try {
        const fileId = Number(req.params.fileId);
        const file = await filesDB.getById(fileId);
        if (!file) {
            return res.status(404).json({ success: false, error: 'Файл не найден' });
        }

        const filePath = path.join(__dirname, 'uploads', `org_${file.org_id}`, file.filename);
        if (!fs.existsSync(filePath)) {
            return res.status(404).json({ success: false, error: 'Файл не найден на сервере' });
        }

        res.setHeader('X-Frame-Options', 'SAMEORIGIN');
        res.setHeader('Content-Security-Policy', "frame-ancestors 'self'");
        res.setHeader('Content-Type', file.mime_type || 'application/octet-stream');
        res.setHeader('Content-Disposition', `inline; filename*=UTF-8''${encodeURIComponent(file.original_name)}`);
        res.setHeader('Cache-Control', 'private, max-age=86400');
        res.sendFile(filePath);
    } catch (err) {
        console.error('Ошибка просмотра файла:', err);
        res.status(500).json({ success: false, error: 'Ошибка сервера при открытии файла' });
    }
});

// Предварительный просмотр файла (таблицы Excel/CSV, XML, TXT, PDF, изображения)
app.get('/api/files/:fileId/preview', requireAuth, async (req, res) => {
    try {
        const fileId = Number(req.params.fileId);
        if (isNaN(fileId)) {
            return res.status(400).json({ success: false, error: 'Неверный идентификатор файла' });
        }

        const file = await filesDB.getById(fileId);
        if (!file) {
            return res.status(404).json({ success: false, error: 'Файл не найден' });
        }

        const filePath = path.join(__dirname, 'uploads', `org_${file.org_id}`, file.filename);
        if (!fs.existsSync(filePath)) {
            return res.status(404).json({ success: false, error: 'Файл не найден на сервере' });
        }

        const filenameExt = path.extname(file.filename || '').toLowerCase();
        const originalExt = path.extname(file.original_name || '').toLowerCase();
        const ext = originalExt || filenameExt;
        const mimeType = (file.mime_type || '').toLowerCase();
        const isDocx = ext === '.docx' || filenameExt === '.docx' || originalExt === '.docx' || mimeType.includes('wordprocessingml');
        const isDoc = ext === '.doc' || filenameExt === '.doc' || originalExt === '.doc' || mimeType.includes('msword');
        const IMAGE_EXTENSIONS = ['.png', '.jpg', '.jpeg', '.webp', '.gif', '.svg', '.bmp'];
        const isImage = IMAGE_EXTENSIONS.includes(ext) || (file.mime_type && file.mime_type.startsWith('image/'));

        // Spreadsheet (.xlsx, .xls, .csv)
        if (ext === '.xlsx' || ext === '.xls' || ext === '.csv') {
            try {
                const workbook = XLSX.readFile(filePath);
                const sheetNames = workbook.SheetNames || [];
                const sheetName = (req.query.sheet && sheetNames.includes(req.query.sheet)) ? req.query.sheet : sheetNames[0];
                const worksheet = sheetName ? workbook.Sheets[sheetName] : null;
                const htmlTable = worksheet ? XLSX.utils.sheet_to_html(worksheet, { id: 'spreadsheetPreviewTable', editable: false }) : '';
                return res.json({
                    success: true,
                    type: 'excel',
                    html: htmlTable,
                    sheetNames: sheetNames,
                    currentSheet: sheetName,
                    originalName: file.original_name
                });
            } catch (parseErr) {
                return res.status(500).json({ success: false, error: 'Ошибка разбора таблицы: ' + parseErr.message });
            }
        }

        // XML (.xml) или текстовые (.txt)
        if (ext === '.xml' || ext === '.txt') {
            try {
                const content = fs.readFileSync(filePath, 'utf-8');
                return res.json({
                    success: true,
                    type: ext === '.xml' ? 'xml' : 'text',
                    content: content,
                    originalName: file.original_name
                });
            } catch (readErr) {
                return res.status(500).json({ success: false, error: 'Ошибка чтения файла: ' + readErr.message });
            }
        }

        // Word DOCX
        if (isDocx) {
            try {
                const result = await mammoth.convertToHtml({ path: filePath });
                return res.json({
                    success: true,
                    type: 'docx',
                    html: result.value || '<p><em>Документ Word не содержит текста или пуст.</em></p>',
                    messages: result.messages,
                    originalName: file.original_name
                });
            } catch (docErr) {
                console.error('Ошибка конвертации docx:', docErr);
                return res.json({
                    success: true,
                    type: 'doc_fallback',
                    originalName: file.original_name,
                    downloadUrl: `/api/files/${fileId}/download`,
                    message: 'Не удалось разобрать разметку DOCX. Скачайте файл для открытия в Microsoft Word.'
                });
            }
        }

        // Word DOC (двоичный формат Word 97-2003)
        if (isDoc) {
            try {
                const extractor = new WordExtractor();
                const extracted = await extractor.extract(filePath);
                const bodyText = (extracted.getBody() || '').trim();
                const headersText = (extracted.getHeaders() || '').trim();
                const footersText = (extracted.getFootnotes() || '').trim();
                let htmlContent = '';
                if (headersText) {
                    htmlContent += `<div style="font-weight:600;margin-bottom:14px;color:#64748b;font-size:13px;border-bottom:1px solid #e2e8f0;padding-bottom:6px;">${escapeHtml(headersText)}</div>`;
                }
                if (bodyText) {
                    const paragraphs = bodyText.split(/\r?\n/).filter(line => line.trim().length > 0);
                    htmlContent += paragraphs.map(p => `<p>${escapeHtml(p)}</p>`).join('');
                }
                if (footersText) {
                    htmlContent += `<div style="margin-top:20px;font-size:12px;color:#94a3b8;border-top:1px solid #e2e8f0;padding-top:8px;">${escapeHtml(footersText)}</div>`;
                }
                if (!htmlContent) {
                    htmlContent = '<p><em>Документ Word (.doc) не содержит текста или пуст.</em></p>';
                }
                return res.json({
                    success: true,
                    type: 'docx', // клиентский lightbox отрендерит через единый lightbox-doc-wrap!
                    html: htmlContent,
                    originalName: file.original_name
                });
            } catch (docErr) {
                console.error('Ошибка чтения .doc:', docErr);
                return res.json({
                    success: true,
                    type: 'doc_fallback',
                    originalName: file.original_name,
                    downloadUrl: `/api/files/${fileId}/download`,
                    message: 'Файл Word формата .doc (двоичный формат). Вы можете скачать его для открытия в приложении Word.'
                });
            }
        }

        // PDF или изображение
        if (ext === '.pdf' || isImage) {
            return res.json({
                success: true,
                type: ext === '.pdf' ? 'pdf' : 'image',
                viewUrl: `/api/files/${fileId}/view`,
                originalName: file.original_name
            });
        }

        // Неподдерживаемый тип для инлайн-просмотра
        return res.json({
            success: false,
            type: 'unsupported',
            originalName: file.original_name,
            downloadUrl: `/api/files/${fileId}/download`
        });
    } catch (err) {
        console.error('Ошибка предварительного просмотра файла:', err);
        res.status(500).json({ success: false, error: 'Ошибка сервера при предварительном просмотре файла' });
    }
});

// Скачать файл
app.get('/api/files/:fileId/download', requireAuth, async (req, res) => {
    try {
        const fileId = Number(req.params.fileId);
        const file = await filesDB.getById(fileId);
        if (!file) {
            return res.status(404).json({ success: false, error: 'Файл не найден' });
        }

        const filePath = path.join(__dirname, 'uploads', `org_${file.org_id}`, file.filename);
        if (!fs.existsSync(filePath)) {
            return res.status(404).json({ success: false, error: 'Файл не найден на сервере' });
        }

        res.download(filePath, file.original_name);
    } catch (err) {
        console.error('Ошибка скачивания файла:', err);
        res.status(500).json({ success: false, error: 'Ошибка сервера при скачивании файла' });
    }
});

// Удалить файл
app.delete('/api/files/:fileId', requireAuth, async (req, res) => {
    try {
        const fileId = Number(req.params.fileId);
        const file = await filesDB.getById(fileId);
        if (!file) {
            return res.status(404).json({ success: false, error: 'Файл не найден' });
        }

        const filePath = path.join(__dirname, 'uploads', `org_${file.org_id}`, file.filename);
        if (fs.existsSync(filePath)) {
            try {
                fs.unlinkSync(filePath);
            } catch (unlinkErr) {
                console.warn('Предупреждение при удалении файла с диска:', unlinkErr.message);
            }
        }

        await filesDB.delete(fileId);
        res.json({ success: true });
    } catch (err) {
        console.error('Ошибка удаления файла:', err);
        res.status(500).json({ success: false, error: 'Ошибка сервера при удалении файла' });
    }
});

// ============================================================
// API: БАЗЫ ДАННЫХ 1С (ЗАКОНСЕРВИРОВАНО В СПРИНТЕ 7)
// ============================================================

/*
// API: Получить все базы 1С организации
app.get('/api/organizations/:id/databases', requireAuth, async (req, res) => {
    try {
        const databases = await databases1CDB.getByOrgId(req.params.id);
        res.json(databases || []);
    } catch (err) {
        console.error('Ошибка получения баз 1С организации:', err.message);
        res.status(500).json({ error: 'Ошибка сервера' });
    }
});

// API: Добавить базу 1С к организации
app.post('/api/organizations/:id/databases', requireAuth, async (req, res) => {
    const { name, release, description, updated_at } = req.body;
    if (!name || !name.trim()) {
        return res.status(400).json({ error: 'Наименование базы обязательно' });
    }
    const descText = (description !== undefined ? description : (release || '')).trim();
    try {
        const newDb = await databases1CDB.create({
            org_id: req.params.id,
            name: name.trim(),
            release: descText,
            description: descText,
            updated_at: updated_at || new Date()
        });
        res.status(201).json(newDb);
    } catch (err) {
        console.error('Ошибка добавления базы 1С:', err.message);
        res.status(500).json({ error: 'Ошибка сервера' });
    }
});

// API: Обновить базу 1С
app.put('/api/databases/:id', requireAuth, async (req, res) => {
    const { name, release, description, updated_at } = req.body;
    if (!name || !name.trim()) {
        return res.status(400).json({ error: 'Наименование базы обязательно' });
    }
    const descText = description !== undefined ? description : release;
    try {
        const affectedRows = await databases1CDB.update(req.params.id, {
            name: name.trim(),
            release: descText !== undefined ? descText.trim() : '',
            description: descText !== undefined ? descText.trim() : '',
            updated_at: updated_at || new Date()
        });
        if (affectedRows === 0) {
            return res.status(404).json({ error: 'База не найдена' });
        }
        res.json({ success: true, message: 'База 1С успешно обновлена' });
    } catch (err) {
        console.error('Ошибка обновления базы 1С:', err.message);
        res.status(500).json({ error: 'Ошибка сервера' });
    }
});

// API: Удалить базу 1С
app.delete('/api/databases/:id', requireAuth, async (req, res) => {
    try {
        const affectedRows = await databases1CDB.delete(req.params.id);
        if (affectedRows === 0) {
            return res.status(404).json({ error: 'База не найдена' });
        }
        res.json({ success: true, message: 'База 1С успешно удалена' });
    } catch (err) {
        console.error('Ошибка удаления базы 1С:', err.message);
        res.status(500).json({ error: 'Ошибка сервера' });
    }
});
*/

// ============================================================
// СОБЫТИЯ (Журнал обновлений и релизов 1С) (ЗАКОНСЕРВИРОВАНО В СПРИНТЕ 7)
// ============================================================

/*
app.get('/events', requireAuth, async (req, res) => {
    try {
        const organizations = await organizationsDB.getAll();
        const allDatabases = typeof databases1CDB.getAll === 'function'
            ? await databases1CDB.getAll()
            : await databases1CDB.getAllRecentUpdates();
        const updates = await databases1CDB.getAllRecentUpdates();

        // Группировка баз по организациям
        const dbsByOrg = {};
        (allDatabases || []).forEach(db => {
            const orgId = Number(db.org_id);
            if (!dbsByOrg[orgId]) dbsByOrg[orgId] = [];
            dbsByOrg[orgId].push(db);
        });

        const groupedOrganizations = (organizations || []).map(org => {
            const dbs = dbsByOrg[org.id] || [];
            return {
                id: org.id,
                name: org.name,
                databases: dbs,
                count: dbs.length
            };
        });

        const totalOrgs = groupedOrganizations.length;
        const totalBases = (allDatabases || []).length;

        res.render('events', {
            organizations: organizations || [],
            groupedOrganizations,
            updates: updates || [],
            totalOrgs,
            totalBases,
            user: req.session.user
        });
    } catch (err) {
        console.error('Ошибка рендеринга страницы событий:', err.message);
        res.status(500).send('Ошибка сервера');
    }
});
*/

// Helper: Очистка HTML до чистого моноширинного текста с сохранением всех строк
function cleanHtmlToPlainText(html) {
    if (!html) return '';
    return html
        .replace(/<style[^>]*>[\s\S]*?<\/style>/gi, '')
        .replace(/<script[^>]*>[\s\S]*?<\/script>/gi, '')
        .replace(/<br\s*[\/]?>/gi, '\n')
        .replace(/<\/p>/gi, '\n')
        .replace(/<\/div>/gi, '\n')
        .replace(/<\/li>/gi, '\n')
        .replace(/<[^>]+>/g, '')
        .replace(/&nbsp;/g, ' ')
        .replace(/&amp;/g, '&')
        .replace(/&lt;/g, '<')
        .replace(/&gt;/g, '>')
        .replace(/&quot;/g, '"')
        .replace(/&#39;/g, "'")
        .trim();
}

// Helper: Безопасное экранирование текста для HTML
function escapeHtml(str) {
    if (!str) return '';
    return String(str)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');
}

// API: Экспорт в TXT
app.get('/api/export/txt/:id', requireAuth, async (req, res) => {
    try {
        const organization = await organizationsDB.getById(req.params.id);
        if (!organization) {
            return res.status(404).send('Организация не найдена');
        }
        
        const cleanText = cleanHtmlToPlainText(organization.content || '');
        const databases = await databases1CDB.getByOrgId(organization.id);

        let fullText = cleanText;
        if (databases && Array.isArray(databases) && databases.length > 0) {
            fullText += '\n\n' + '='.repeat(50) + '\nИНФОРМАЦИОННЫЕ БАЗЫ 1С:\n' + '='.repeat(50) + '\n';
            databases.forEach((db, idx) => {
                const desc = db.description || db.release || '—';
                const dateStr = db.updated_at ? new Date(db.updated_at).toLocaleDateString('ru-RU') : '—';
                fullText += `${idx + 1}. ${db.name}\n   Описание: ${desc}\n   Дата: ${dateStr}\n`;
            });
        }

        const safeName = organization.name.replace(/[/\\?%*:|"<>]/g, '_');
        const filename = `${safeName}.txt`;
        const encodedFilename = encodeURIComponent(filename);

        res.setHeader('Content-Type', 'text/plain; charset=utf-8');
        res.setHeader('Content-Disposition', `attachment; filename="${encodedFilename}"; filename*=UTF-8''${encodedFilename}`);
        res.send(fullText);
    } catch (error) {
        console.error('Ошибка экспорта TXT:', error.message);
        return res.status(500).json({ error: 'Ошибка экспорта в TXT' });
    }
});

// API: Экспорт в PDF (Printable View с @media print и window.print())
app.get('/api/export/pdf/:id', requireAuth, async (req, res) => {
    try {
        const organization = await organizationsDB.getById(req.params.id);
        
        if (!organization) {
            return res.status(404).send('Организация не найдена');
        }

        const databases = await databases1CDB.getByOrgId(organization.id);
        organization.bases = databases || [];

        const user = req.session.user || { username: 'admin' };
        const author = user.username === 'admin' ? 'Администратор' : user.username;
        const cleanAuthor = escapeHtml(author);
        const cleanName = escapeHtml(organization.name);
        const cleanContent = escapeHtml(cleanHtmlToPlainText(organization.content || ''));
        
        const exportDate = new Date().toLocaleString('ru-RU', {
            day: '2-digit',
            month: '2-digit',
            year: 'numeric',
            hour: '2-digit',
            minute: '2-digit'
        });

        let basesHtml = '';
        if (organization.bases && Array.isArray(organization.bases) && organization.bases.length > 0) {
            basesHtml = `
                <div class="print-section-title">Информационные базы 1С</div>
                <table class="print-table">
                    <thead>
                        <tr>
                            <th>Информационная база</th>
                            <th>Описание</th>
                            <th>Дата обновления</th>
                        </tr>
                    </thead>
                    <tbody>
                        ${organization.bases.map(b => `
                            <tr>
                                <td>${escapeHtml(b.name || '')}</td>
                                <td>${escapeHtml(b.description || b.release || '—')}</td>
                                <td>${escapeHtml(b.updated_at ? new Date(b.updated_at).toLocaleDateString('ru-RU') : '—')}</td>
                            </tr>
                        `).join('')}
                    </tbody>
                </table>
            `;
        }

        const htmlContent = `<!DOCTYPE html>
<html lang="ru">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>${cleanName} — Печать / Сохранить в PDF</title>
    <link rel="preconnect" href="https://fonts.googleapis.com">
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
    <link href="https://fonts.googleapis.com/css2?family=JetBrains+Mono:wght@400;500;600&family=Inter:wght@400;500;600;700&display=swap" rel="stylesheet">
    <style>
        * {
            box-sizing: border-box;
            margin: 0;
            padding: 0;
        }
        body {
            font-family: 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
            background: #f1f5f9;
            color: #0f172a;
            line-height: 1.5;
            -webkit-print-color-adjust: exact;
            print-color-adjust: exact;
        }
        .print-topbar {
            position: sticky;
            top: 0;
            z-index: 100;
            display: flex;
            align-items: center;
            justify-content: space-between;
            background: #ffffff;
            border-bottom: 1px solid #e2e8f0;
            padding: 12px 24px;
            box-shadow: 0 1px 3px rgba(0, 0, 0, 0.05);
        }
        .print-back-link {
            display: inline-flex;
            align-items: center;
            gap: 6px;
            color: #475569;
            text-decoration: none;
            font-size: 14px;
            font-weight: 500;
            transition: color 0.15s;
        }
        .print-back-link:hover {
            color: #0f172a;
        }
        .print-btn {
            display: inline-flex;
            align-items: center;
            gap: 8px;
            background: #2563eb;
            color: #ffffff;
            border: none;
            border-radius: 6px;
            padding: 8px 18px;
            font-size: 14px;
            font-weight: 600;
            cursor: pointer;
            box-shadow: 0 1px 2px rgba(37, 99, 235, 0.2);
            transition: background 0.15s;
        }
        .print-btn:hover {
            background: #1d4ed8;
        }
        .print-wrapper {
            max-width: 860px;
            margin: 32px auto;
            padding: 0 20px 40px;
        }
        .print-page {
            background: #ffffff;
            border: 1px solid #e2e8f0;
            border-radius: 8px;
            padding: 48px;
            box-shadow: 0 4px 12px rgba(15, 23, 42, 0.04);
        }
        .print-header {
            border-bottom: 2px solid #0f172a;
            padding-bottom: 16px;
            margin-bottom: 24px;
        }
        .print-title {
            font-size: 26px;
            font-weight: 700;
            color: #0f172a;
            margin-bottom: 8px;
        }
        .print-meta {
            font-size: 13px;
            color: #64748b;
            display: flex;
            gap: 16px;
            flex-wrap: wrap;
        }
        .print-section-title {
            font-size: 13px;
            text-transform: uppercase;
            letter-spacing: 0.05em;
            color: #64748b;
            font-weight: 700;
            margin-bottom: 10px;
            margin-top: 24px;
        }
        .print-notes {
            font-family: 'JetBrains Mono', Consolas, 'SF Mono', Monaco, monospace;
            font-size: 13px;
            line-height: 1.6;
            white-space: pre-wrap;
            word-break: break-word;
            background: #f8fafc;
            border: 1px solid #e2e8f0;
            border-radius: 6px;
            padding: 20px;
            color: #1e293b;
        }
        .print-table {
            width: 100%;
            border-collapse: collapse;
            font-size: 13px;
            margin-top: 8px;
        }
        .print-table th, .print-table td {
            padding: 10px 14px;
            border: 1px solid #cbd5e1;
            text-align: left;
        }
        .print-table th {
            background: #f1f5f9;
            font-weight: 600;
            color: #1e293b;
        }
        .print-table tr:nth-child(even) td {
            background: #f8fafc;
        }
        @media print {
            body {
                background: #ffffff !important;
                color: #000000 !important;
            }
            .no-print, .print-topbar {
                display: none !important;
            }
            .print-wrapper {
                max-width: 100% !important;
                margin: 0 !important;
                padding: 0 !important;
            }
            .print-page {
                border: none !important;
                box-shadow: none !important;
                padding: 0 !important;
                border-radius: 0 !important;
            }
            .print-header {
                border-bottom: 2px solid #000000 !important;
            }
            .print-notes {
                background: #ffffff !important;
                border: 1px solid #999999 !important;
                color: #000000 !important;
                font-size: 10pt !important;
                line-height: 1.5 !important;
                padding: 12px !important;
            }
            .print-table th {
                background: #eeeeee !important;
                color: #000000 !important;
            }
            @page {
                margin: 15mm 15mm 15mm 15mm;
                size: A4 portrait;
            }
        }
    </style>
</head>
<body>
    <div class="print-topbar no-print">
        <a href="/wiki/organization/${organization.id}" class="print-back-link">← Вернуться в Wiki</a>
        <button onclick="window.print()" class="print-btn">🖨 Напечатать / Сохранить как PDF</button>
    </div>

    <div class="print-wrapper">
        <div class="print-page">
            <header class="print-header">
                <h1 class="print-title">${cleanName}</h1>
                <div class="print-meta">
                    <span><strong>Дата экспорта:</strong> ${exportDate}</span>
                    <span><strong>Автор:</strong> ${cleanAuthor}</span>
                    <span><strong>Система:</strong> ИТС-Ком Wiki</span>
                </div>
            </header>

            <div class="print-section-title">Параметры и заметки сервера</div>
            <pre class="print-notes">${cleanContent || 'Заметки не заполнены'}</pre>

            ${basesHtml}
        </div>
    </div>

    <script>
        window.addEventListener('DOMContentLoaded', () => {
            setTimeout(() => { window.print(); }, 400);
        });
    </script>
</body>
</html>`;

        res.setHeader('Content-Type', 'text/html; charset=utf-8');
        res.send(htmlContent);
        
    } catch (error) {
        console.error('Ошибка экспорта PDF:', error.message);
        res.status(500).json({ error: 'Ошибка экспорта в PDF' });
    }
});

// Запуск сервера
const serverInstance = app.listen(PORT, () => {
    console.log(`\n${'='.repeat(50)}`);
    console.log(`  🚀 Wiki-сервер запущен успешно!`);
    console.log(`  📍 URL: http://localhost:${PORT}`);
    console.log(`  📍 Локальная сеть: http://<IP-адрес>:${PORT}`);
    console.log(`${'='.repeat(50)}\n`);
});

module.exports = {
    app,
    serverInstance,
    getOrgMonogram,
    checkAuthRateLimit,
    isRateLimited,
    clearRateLimit,
    recordFailedAttempt,
    failedAttemptsMap,
    userPinsDB,
    filesDB
};


