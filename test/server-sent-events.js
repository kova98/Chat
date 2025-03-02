// Long Polling load test

import http from 'k6/http';
import { sleep } from 'k6';

export const options = {
    vus: 5000,
    duration: '30s',
};

export function setup () {
    // Initial connection
    const rnd = Math.floor(Math.random() * 1000000);
    const user = `user${rnd}`;
    let lpUrl = `http://localhost:5000/sse?name=${user}`;
    const params = {
        headers: {
            'Content-Type': 'application/json',
        },
    };

    const response = http.get(lpUrl, params);
    
    return { user, };
}

export default function (data) {
    const rnd = Math.floor(Math.random() * 1000000);
    const msg = `msg${rnd}`;
    const body = JSON.stringify({
        "Name": data.user,
        "Content": msg,
    });
    const params = {
        headers: {
            'Content-Type': 'application/json',
        },
    };

    const msgUrl = `http://localhost:5000/lp/message`;
    http.post(msgUrl, body, params);
    
    sleep(1);
}
