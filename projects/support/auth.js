const mysql = require('mysql2/promise');
const bcrypt = require('bcrypt');

// Подключение к MariaDB
const pool = mysql.createPool({
    host: process.env.DB_HOST || 'localhost',
    port: process.env.DB_PORT || 3306,
    user: process.env.DB_USER || 'p-14802_adminits',
    password: process.env.DB_PASSWORD || 'ASDFzxcv@113525',
    database: process.env.DB_NAME || 'p-14802_wiki',
    waitForConnections: true,
    connectionLimit: 10,
    queueLimit: 0
});


// Инициализация таблицы пользователей
async function initDatabase() {
    let connection;
    try {
        connection = await pool.getConnection();

        await connection.query(`
            CREATE TABLE IF NOT EXISTS users (
                id INT AUTO_INCREMENT PRIMARY KEY,
                username VARCHAR(50) UNIQUE NOT NULL,
                password_hash VARCHAR(255) NOT NULL,
                email VARCHAR(255) DEFAULT NULL,
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                last_login TIMESTAMP
            )
        `);

        await connection.query(`
            ALTER TABLE users ADD COLUMN IF NOT EXISTS email VARCHAR(255) DEFAULT NULL
        `);

        console.log('✓ Таблица users готова');
        
        const [rows] = await connection.query('SELECT COUNT(*) as count FROM users');
        const count = rows[0].count;
        
        if (count === 0) {
            const adminPassword = await bcrypt.hash('admin123', 10);
            await connection.query(
                'INSERT INTO users (username, password_hash) VALUES (?, ?)',
                ['admin', adminPassword]
            );
            console.log('✓ Создан администратор по умолчанию (admin / admin123)');
        }
        
    } catch (err) {
        console.error('Ошибка инициализации базы данных:', err.message);
        throw err;
    } finally {
        if (connection) connection.release();
    }
}

// Регистрация нового пользователя
async function registerUser(username, password) {
    let connection;
    try {
        connection = await pool.getConnection();

        const [checkResult] = await connection.query(
            'SELECT id FROM users WHERE username = ?',
            [username]
        );
        
        if (checkResult.length > 0) {
            return { success: false, error: 'Пользователь с таким логином уже существует' };
        }
        
        if (!username || username.trim().length < 3) {
            return { success: false, error: 'Логин должен содержать минимум 3 символа' };
        }
        
        if (!password || password.length < 5) {
            return { success: false, error: 'Пароль должен содержать минимум 5 символов' };
        }
        
        const passwordHash = await bcrypt.hash(password, 10);
        
        const [result] = await connection.query(
            'INSERT INTO users (username, password_hash) VALUES (?, ?)',
            [username.trim(), passwordHash]
        );
        
        const [newUser] = await connection.query(
            'SELECT id, username, created_at FROM users WHERE id = ?',
            [result.insertId]
        );

        console.log(`✓ Зарегистрирован новый пользователь: ${username}`);
        
        return { 
            success: true, 
            user: newUser[0]
        };
        
    } catch (err) {
        console.error('Ошибка регистрации пользователя:', err.message);
        return { success: false, error: 'Ошибка сервера при регистрации' };
    } finally {
        if (connection) connection.release();
    }
}

// Аутентификация пользователя
async function authenticateUser(username, password) {
    let connection;
    try {
        connection = await pool.getConnection();

        const [rows] = await connection.query(
            'SELECT id, username, password_hash FROM users WHERE username = ?',
            [username]
        );
        
        if (rows.length === 0) {
            return { success: false, error: 'Неверный логин или пароль' };
        }
        
        const user = rows[0];
        
        const passwordMatch = await bcrypt.compare(password, user.password_hash);
        
        if (!passwordMatch) {
            return { success: false, error: 'Неверный логин или пароль' };
        }
        
        await connection.query(
            'UPDATE users SET last_login = CURRENT_TIMESTAMP WHERE id = ?',
            [user.id]
        );
        
        console.log(`✓ Успешный вход: ${username}`);
        
        return { 
            success: true, 
            user: {
                id: user.id,
                username: user.username
            }
        };
        
    } catch (err) {
        console.error('Ошибка аутентификации:', err.message);
        return { success: false, error: 'Ошибка сервера при входе' };
    } finally {
        if (connection) connection.release();
    }
}

// Получение пользователя по ID
async function getUserById(userId) {
    let connection;
    try {
        connection = await pool.getConnection();

        const [rows] = await connection.query(
            'SELECT id, username, email, created_at, last_login FROM users WHERE id = ?',
            [userId]
        );
        
        if (rows.length === 0) {
            return null;
        }
        
        return rows[0];
        
    } catch (err) {
        console.error('Ошибка получения пользователя:', err.message);
        return null;
    } finally {
        if (connection) connection.release();
    }
}

// Обновление профиля пользователя
async function updateProfile(userId, { email }) {
    let connection;
    try {
        connection = await pool.getConnection();
        await connection.query(
            'UPDATE users SET email = ? WHERE id = ?',
            [email ? email.trim() : null, userId]
        );
        return { success: true };
    } catch (err) {
        console.error('Ошибка обновления профиля:', err.message);
        return { success: false, error: 'Ошибка сервера при обновлении профиля' };
    } finally {
        if (connection) connection.release();
    }
}

// Получение всех пользователей (для админки)
async function getAllUsers() {
    let connection;
    try {
        connection = await pool.getConnection();

        const [rows] = await connection.query(
            'SELECT id, username, created_at, last_login FROM users ORDER BY created_at DESC'
        );
        
        return rows;
        
    } catch (err) {
        console.error('Ошибка получения пользователей:', err.message);
        return [];
    } finally {
        if (connection) connection.release();
    }
}

// Проверка существования пользователя по логину (Спринт 7)
async function checkUserExists(username) {
    if (!username || typeof username !== 'string' || !username.trim()) {
        return { exists: false };
    }
    let connection;
    try {
        connection = await pool.getConnection();
        const [rows] = await connection.query(
            'SELECT id FROM users WHERE LOWER(username) = LOWER(?)',
            [username.trim()]
        );
        return { exists: rows.length > 0 };
    } catch (err) {
        console.error('Ошибка проверки пользователя:', err.message);
        return { exists: false, error: 'Ошибка сервера при проверке пользователя' };
    } finally {
        if (connection) connection.release();
    }
}

// Сброс пароля пользователя (Спринт 7)
async function resetPassword(username, newPassword) {
    if (!username || typeof username !== 'string' || !username.trim()) {
        return { success: false, error: 'Логин не указан' };
    }
    if (!newPassword || typeof newPassword !== 'string' || newPassword.length < 8) {
        return { success: false, error: 'Пароль должен содержать минимум 8 символов' };
    }
    let connection;
    try {
        connection = await pool.getConnection();
        const [rows] = await connection.query(
            'SELECT id FROM users WHERE LOWER(username) = LOWER(?)',
            [username.trim()]
        );
        if (rows.length === 0) {
            return { success: false, error: 'Пользователь не найден' };
        }
        const passwordHash = await bcrypt.hash(newPassword, 10);
        await connection.query(
            'UPDATE users SET password_hash = ? WHERE id = ?',
            [passwordHash, rows[0].id]
        );
        console.log(`✓ Пароль пользователя ${username.trim()} успешно обновлен`);
        return { success: true };
    } catch (err) {
        console.error('Ошибка сброса пароля:', err.message);
        return { success: false, error: 'Ошибка сервера при сбросе пароля' };
    } finally {
        if (connection) connection.release();
    }
}

module.exports = {
    pool,
    initDatabase,
    registerUser,
    authenticateUser,
    getUserById,
    updateProfile,
    getAllUsers,
    checkUserExists,
    resetPassword
};

