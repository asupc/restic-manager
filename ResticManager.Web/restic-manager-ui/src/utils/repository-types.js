// 仓库类型元数据：与后端 RepositoryTypes 白名单、ResticEnvironmentBuilder 的凭据键一一对应。
// locationFields 描述按类型拼装 restic location（--repo 的值）的结构化字段；
// credentialFields 描述存入 credentialsJson 的凭据字段（key 必须与后端映射一致）。

export const REPOSITORY_TYPES = ['local', 'sftp', 'rest', 's3', 'b2', 'azure', 'gs', 'rclone'];

export const REPOSITORY_TYPE_META = {
    local: {
        label: '本地目录',
        icon: 'el-icon-folder',
        desc: '存储在本机磁盘目录中的仓库',
        locationFields: [{ key: 'path', label: '仓库路径', required: true, placeholder: '选择或输入仓库存储路径', span: 24 }],
        credentialFields: []
    },
    sftp: {
        label: 'SFTP',
        icon: 'el-icon-connection',
        desc: '通过 SSH 连接远程服务器。需预先在本机配置到目标的 SSH 密钥免密登录（不支持交互输入密码）',
        locationFields: [
            { key: 'user', label: 'SSH 用户', required: true, placeholder: 'restic' },
            { key: 'host', label: '主机地址', required: true, placeholder: '192.168.1.10' },
            { key: 'port', label: '端口', required: false, placeholder: '可空，默认 22' },
            { key: 'path', label: '远端路径', required: true, placeholder: '/srv/restic-repo（远端绝对路径）' }
        ],
        credentialFields: []
    },
    rest: {
        label: 'REST Server',
        icon: 'el-icon-link',
        desc: '连接自建的 restic REST server，认证凭据与仓库加密密码相互独立',
        locationFields: [{ key: 'url', label: '服务器地址', required: true, placeholder: 'https://rest.example.com:8000', span: 24 }],
        credentialFields: [
            { key: 'username', label: '认证用户名', required: false, placeholder: 'REST server 认证用户名（未启用认证可留空）' },
            { key: 'password', label: '认证密码', required: false, placeholder: 'REST server 认证密码', type: 'password' }
        ]
    },
    s3: {
        label: 'S3 兼容存储',
        icon: 'el-icon-s-platform',
        desc: 'AWS S3、MinIO、Wasabi 等 S3 兼容对象存储',
        locationFields: [
            { key: 'endpoint', label: 'Endpoint', required: true, placeholder: 's3.us-east-1.amazonaws.com（MinIO 填 http://localhost:9000）' },
            { key: 'bucket', label: '存储桶', required: true, placeholder: 'bucket 名称，可含子路径如 backups/restic' }
        ],
        credentialFields: [
            { key: 'accessKey', label: 'Access Key', required: true, placeholder: '对应 AWS_ACCESS_KEY_ID' },
            { key: 'secretKey', label: 'Secret Key', required: true, placeholder: '对应 AWS_SECRET_ACCESS_KEY', type: 'password' }
        ]
    },
    b2: {
        label: 'Backblaze B2',
        icon: 'el-icon-s-data',
        desc: 'Backblaze B2 云存储',
        locationFields: [
            { key: 'bucket', label: 'Bucket', required: true, placeholder: 'bucketName' },
            { key: 'path', label: '子路径', required: false, placeholder: '可空，默认根' }
        ],
        credentialFields: [
            { key: 'accountId', label: 'Account ID', required: true, placeholder: '对应 B2_ACCOUNT_ID' },
            { key: 'accountKey', label: 'Account Key', required: true, placeholder: '对应 B2_ACCOUNT_KEY', type: 'password' }
        ]
    },
    azure: {
        label: 'Azure Blob',
        icon: 'el-icon-s-grid',
        desc: 'Azure Blob Storage',
        locationFields: [
            { key: 'container', label: '容器', required: true, placeholder: '容器名' },
            { key: 'path', label: '子路径', required: false, placeholder: '可空，默认根' }
        ],
        credentialFields: [
            { key: 'accountName', label: '账户名', required: true, placeholder: '对应 AZURE_ACCOUNT_NAME' },
            { key: 'accountKey', label: '账户密钥', required: true, placeholder: '对应 AZURE_ACCOUNT_KEY', type: 'password' }
        ]
    },
    gs: {
        label: 'Google Cloud Storage',
        icon: 'el-icon-s-marketing',
        desc: 'GCS。密钥 JSON 文件需预先放在服务器上（容器部署时挂载进容器）',
        locationFields: [
            { key: 'bucket', label: 'Bucket', required: true, placeholder: 'bucket 名' },
            { key: 'path', label: '子路径', required: false, placeholder: '可空，默认根' }
        ],
        credentialFields: [
            { key: 'projectId', label: 'Project ID', required: true, placeholder: 'GCP 项目 ID' },
            { key: 'credentialsPath', label: '密钥文件路径', required: true, placeholder: '服务器上 GCP JSON 密钥文件绝对路径，如 /data/keys/gcs.json' }
        ]
    },
    rclone: {
        label: 'rclone',
        icon: 'el-icon-refresh',
        desc: '通过 rclone 支持各类网盘等更多后端。需在服务器上安装 rclone（PATH 可用）并配置好 remote',
        locationFields: [{ key: 'remote', label: 'remote:子路径', required: true, placeholder: 'remote名:子路径，如 b2prod:yggdrasil', span: 24 }],
        credentialFields: []
    }
};

function v(fields, key) {
    return ((fields || {})[key] || '').trim();
}

function stripSlashes(s) {
    return s.replace(/^\/+|\/+$/g, '');
}

/**
 * 按类型把结构化字段拼装为完整 restic location（--repo 的值）。
 * 必填字段不完整时返回 ''。
 */
export function buildLocation(type, fields) {
    switch (type) {
        case 'local':
            return v(fields, 'path');
        case 'sftp': {
            const user = v(fields, 'user');
            const host = v(fields, 'host');
            const port = v(fields, 'port');
            const path = v(fields, 'path');
            if (!user || !host || !path) return '';
            // 冒号形式（无端口）：sftp:user@host:/srv/restic-repo
            if (!port) return `sftp:${user}@${host}:${path.startsWith('/') ? path : '/' + path}`;
            // URL 形式（带端口）：绝对路径必须双斜杠（第一个斜杠分隔连接串，第二个才是路径起点）
            return `sftp://${user}@${host}:${port}//${stripSlashes(path)}`;
        }
        case 'rest': {
            const url = v(fields, 'url');
            return url ? `rest:${url}` : '';
        }
        case 's3': {
            const endpoint = v(fields, 'endpoint');
            const bucket = stripSlashes(v(fields, 'bucket'));
            if (!endpoint || !bucket) return '';
            return `s3:${endpoint}/${bucket}`;
        }
        case 'b2': {
            const bucket = v(fields, 'bucket');
            if (!bucket) return '';
            const path = stripSlashes(v(fields, 'path'));
            return path ? `b2:${bucket}:${path}` : `b2:${bucket}`;
        }
        case 'azure':
        case 'gs': {
            const bucket = v(fields, 'bucket') || v(fields, 'container');
            if (!bucket) return '';
            const path = stripSlashes(v(fields, 'path'));
            return `${type}:${bucket}:/${path}`;
        }
        case 'rclone': {
            const remote = v(fields, 'remote');
            if (!remote) return '';
            // 用户可能误带 rclone: 前缀，去掉避免重复
            return `rclone:${remote.replace(/^rclone:/, '')}`;
        }
        default:
            return '';
    }
}

/**
 * 把已有 location 反解析回结构化字段（编辑回显用）。
 * 尽力解析，无法识别时返回 null（由调用方提示用户重新填写）。
 */
export function parseLocation(type, path) {
    if (!path) return null;
    switch (type) {
        case 'local':
            return { path };
        case 'sftp': {
            // URL 形式：sftp://user@host:2222//srv/restic-repo（路径含双斜杠）
            let m = path.match(/^sftp:\/\/([^@/]+)@([^:/]+):(\d+)\/(.*)$/);
            if (m) return { user: m[1], host: m[2], port: m[3], path: '/' + m[4].replace(/^\/+/, '') };
            // 冒号形式：sftp:user@host:/srv/restic-repo
            m = path.match(/^sftp:([^@/]+)@([^:/]+):(\/.+)$/);
            if (m) return { user: m[1], host: m[2], port: '', path: m[3] };
            return null;
        }
        case 'rest':
            return path.startsWith('rest:') ? { url: path.slice(5) } : null;
        case 's3': {
            const rest = path.replace(/^s3:/, '');
            // endpoint 可带 scheme（http://localhost:9000），host 之后第一个 / 起为 bucket（可含子路径）
            const m = rest.match(/^(https?:\/\/)?([^/]+)(\/.+)$/);
            if (!m) return null;
            return { endpoint: (m[1] || '') + m[2], bucket: m[3].replace(/^\/+/, '') };
        }
        case 'b2': {
            const rest = path.replace(/^b2:/, '');
            const idx = rest.indexOf(':');
            if (idx < 0) return rest ? { bucket: rest } : null;
            return { bucket: rest.slice(0, idx), path: rest.slice(idx + 1) };
        }
        case 'azure':
        case 'gs': {
            const rest = path.replace(new RegExp('^' + type + ':'), '');
            const idx = rest.indexOf(':');
            if (idx < 0) return rest ? { bucket: rest, container: rest, path: '' } : null;
            const bucket = rest.slice(0, idx);
            return { bucket, container: bucket, path: rest.slice(idx + 1).replace(/^\/+/, '') };
        }
        case 'rclone':
            return path.startsWith('rclone:') ? { remote: path.slice(7) } : null;
        default:
            return null;
    }
}

export function typeIcon(type) {
    return (REPOSITORY_TYPE_META[type] || REPOSITORY_TYPE_META.local).icon;
}
