x = (1:1000)/1000;
x_20k = (1:20000)/20000;
fs = 440;

y = sin(x*2*fs*pi);
%sound(y, 1000);

subplot(1,2,1);

plot(x_20k, sin(x_20k*2*fs*pi));
hold on
scatter(x, y);

xlim([0 0.01]);
[t,s] = title('440Hz');
t.FontSize = 16;

y1000 = sin(x*2*(fs + 1000)*pi);
%sound(y1000, 1000);

subplot(1,2,2);

plot(x_20k, sin(x_20k*2*(fs + 1000)*pi));
hold on
scatter(x, y1000);

xlim([0 0.01]);
[t,s] = title('1440Hz');
t.FontSize = 16;