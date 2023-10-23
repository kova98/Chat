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
    let lpUrl = `http://localhost:5000/lp?name=${user}`;
    const params = {
        headers: {
            'Content-Type': 'application/json',
        },
    };

    const response = http.get(lpUrl, params);
    const id = response.headers['X-Connection-Id']
    
    return { user, lpUrl, id };
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

    // Wait for messages
    const lpUrl = `http://localhost:5000/lp?name=${data.user}&id=${data.id}`;
    const res = http.get(lpUrl, params);
    
    sleep(1);
}
