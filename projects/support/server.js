require('dotenv').config();
const express = require('express');
const bodyParser = require('body-parser');
const session = require('express-session');
const path = require('path');
const { marked } = require('marked');
const createDOMPurify = require('dompurify');
const { JSDOM } = require('jsdom');

const USE_STUB = process.env.DB_STUB === 'true' || process.env.DB_STUB === '1';

let pool, initDatabase, registerUser, authenticateUser, getUserById, updateProfile, initOrganizationTable, organizationsDB, initDatabases1CTable, databases1CDB;

if (USE_STUB) {
    console.log('⚠️  Using in-memory DB stub (DB_STUB=true)');
    const stub = require('./db-stub');
    pool = stub.pool;
    initDatabase = stub.initDatabase;
    registerUser = stub.registerUser;
    authenticateUser = stub.authenticateUser;
    getUserById = stub.getUserById;
    updateProfile = stub.updateProfile;
    initOrganizationTable = stub.initOrganizationTable;
    organizationsDB = stub.organizationsDB;
    initDatabases1CTable = stub.initDatabases1CTable;
    databases1CDB = stub.databases1CDB;
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
    initOrganizationTable = dbModule.initOrganizationTable;
    organizationsDB = dbModule.organizationsDB;
    initDatabases1CTable = dbModule.initDatabases1CTable;
    databases1CDB = dbModule.databases1CDB;
}

const app = express();
const PORT = process.env.PORT || 3000;

// Required for Plesk/Nginx reverse proxy — makes req.secure and req.ip work correctly
app.set('trust proxy', 1);

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

// Middleware для проверки авторизации
function requireAuth(req, res, next) {
    if (req.session && req.session.user) {
        next();
    } else {
        res.redirect('/login');
    }
}

// Страница логина
app.get('/login', (req, res) => {
    if (req.session && req.session.user) {
        return res.redirect('/');
    }
    res.render('login', { error: null });
});

// Обработка логина
app.post('/login', async (req, res) => {
    const { username, password, remember } = req.body;
    
    try {
        const result = await authenticateUser(username, password);
        
        if (result.success) {
            req.session.user = {
                id: result.user.id,
                username: result.user.username,
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
                    return res.render('login', { error: 'Ошибка сессии. Попробуйте ещё раз.' });
                }
                res.redirect('/');
            });
        } else {
            res.render('login', { 
                error: result.error || 'Ошибка входа' 
            });
        }
    } catch (err) {
        console.error('Ошибка при входе:', err);
        res.render('login', { 
            error: 'Ошибка сервера. Попробуйте позже.' 
        });
    }
});

// Страница регистрации
app.get('/register', (req, res) => {
    if (req.session && req.session.user) {
        return res.redirect('/');
    }
    res.render('register', { error: null, success: null });
});

// Обработка регистрации
app.post('/register', async (req, res) => {
    const { username, password, confirmPassword } = req.body;
    
    try {
        // Проверка совпадения паролей
        if (password !== confirmPassword) {
            return res.render('register', { 
                error: 'Пароли не совпадают',
                success: null
            });
        }
        
        const result = await registerUser(username, password);
        
        if (result.success) {
            req.session.user = {
                id: result.user.id,
                username: result.user.username,
                loginTime: new Date()
            };

            req.session.save((err) => {
                if (err) {
                    console.error('Ошибка сохранения сессии:', err);
                    return res.render('register', { error: 'Ошибка сессии. Попробуйте ещё раз.', success: null });
                }
                res.redirect('/');
            });
        } else {
            res.render('register', { 
                error: result.error || 'Ошибка регистрации',
                success: null
            });
        }
    } catch (err) {
        console.error('Ошибка при регистрации:', err);
        res.render('register', { 
            error: 'Ошибка сервера. Попробуйте позже.',
            success: null
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
app.get('/', requireAuth, async (req, res) => {
    try {
        const organizations = await organizationsDB.getAll();
        const serverCount = Array.isArray(organizations) ? organizations.length : 25;
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
            currentDateStr
        });
    } catch (err) {
        console.error('Ошибка рендеринга главной страницы:', err);
        res.render('home', { 
            user: req.session.user,
            serverCount: 25,
            currentDateStr: 'Четверг, 22 октября 2026 г.'
        });
    }
});

// Страница Wiki (список организаций)
app.get('/wiki', requireAuth, async (req, res) => {
    try {
        const organizations = await organizationsDB.getAll();
        res.render('wiki', { 
            organizations, 
            currentOrg: null,
            databases: [],
            user: req.session.user
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
        
        res.render('wiki', { 
            organizations, 
            currentOrg: organization,
            databases: databases || [],
            user: req.session.user
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
// API: БАЗЫ ДАННЫХ 1С
// ============================================================

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
    const { name, release, updated_at } = req.body;
    if (!name || !name.trim()) {
        return res.status(400).json({ error: 'Наименование базы обязательно' });
    }
    try {
        const newDb = await databases1CDB.create({
            org_id: req.params.id,
            name: name.trim(),
            release: release ? release.trim() : '',
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
    const { name, release, updated_at } = req.body;
    if (!name || !name.trim()) {
        return res.status(400).json({ error: 'Наименование базы обязательно' });
    }
    try {
        const affectedRows = await databases1CDB.update(req.params.id, {
            name: name.trim(),
            release: release ? release.trim() : '',
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

// ============================================================
// СОБЫТИЯ (Журнал обновлений и релизов 1С)
// ============================================================
app.get('/events', requireAuth, async (req, res) => {
    try {
        const organizations = await organizationsDB.getAll();
        const updates = await databases1CDB.getAllRecentUpdates();
        res.render('events', {
            organizations: organizations || [],
            updates: updates || [],
            user: req.session.user
        });
    } catch (err) {
        console.error('Ошибка рендеринга страницы событий:', err.message);
        res.status(500).send('Ошибка сервера');
    }
});

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
        const safeName = organization.name.replace(/[/\\?%*:|"<>]/g, '_');
        const filename = `${safeName}.txt`;
        const encodedFilename = encodeURIComponent(filename);

        res.setHeader('Content-Type', 'text/plain; charset=utf-8');
        res.setHeader('Content-Disposition', `attachment; filename="${encodedFilename}"; filename*=UTF-8''${encodedFilename}`);
        res.send(cleanText);
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
                            <th>Наименование базы</th>
                            <th>Релиз конфигурации</th>
                            <th>Дата обновления</th>
                        </tr>
                    </thead>
                    <tbody>
                        ${organization.bases.map(b => `
                            <tr>
                                <td>${escapeHtml(b.name || '')}</td>
                                <td>${escapeHtml(b.release || '')}</td>
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

module.exports = { app, serverInstance };


