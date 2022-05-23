import requests
import argparse

from sklearn.preprocessing import MinMaxScaler

import numpy as np
import pandas as pd
import tensorflow as tf

def create_day_year_signal(data_frame):
    dateTime = pd.to_datetime(data_frame.index, format='%d.%m.%Y %H:%M:%S')

    timestamp = dateTime.map(pd.Timestamp.timestamp)

    day = 24*60*60
    year = (365.2425)*day

    data_frame['sin(day)'] = np.sin(timestamp * (2 * np.pi / day))
    data_frame['cos(day)'] = np.cos(timestamp * (2 * np.pi / day))
    data_frame['sin(year)'] = np.sin(timestamp * (2 * np.pi / year))
    data_frame['cos(year)'] = np.cos(timestamp * (2 * np.pi / year))

def create_dataset(data_frame, n_deterministic_features,
                   window_size, forecast_size,
                   batch_size, shuffle):
    shuffle_buffer_size = len(data_frame)

    # Total size of window is given by the number of steps to be considered
    # before prediction time + steps that we want to forecast
    total_size = window_size + forecast_size

    tensor = tf.convert_to_tensor(data_frame, dtype=tf.float32) # ADDED
    data = tf.data.Dataset.from_tensor_slices(tensor)

    # Selecting windows
    data = data.window(total_size, shift=1, drop_remainder=True)
    data = data.flat_map(lambda k: k.batch(total_size))

    # Shuffling data with seed
    if shuffle == True:
        data = data.shuffle(shuffle_buffer_size, seed=42)

    # Extracting past features + deterministic future + labels
    data = data.map(lambda k: ((k[:-forecast_size],
                                k[-forecast_size:, -n_deterministic_features:]),
                               k[-forecast_size:, 0]))

    return data.batch(batch_size).prefetch(tf.data.experimental.AUTOTUNE)

# Default global values
WINDOW_LENGTH = 24*7 # How much data (hours) from the past should we need for a forecast?
HORIZON = 24*1 # How far ahead (hours) do we want to generate forecasts?
BATCH_SIZE = 32
SHUFFLE = True
EPOCHS = 5
LATENT_DIMENSION = 16
HIDDEN_LAYERS = 2
HIDDEN_DIMENSION = 16
HIDDEN_ACTIVATION = 'relu'
TRAIN_VAL_SPLIT = 70
VAL_TEST_SPLIT = 90

# Parsing arguments from command line
parser = argparse.ArgumentParser()
parser.add_argument('-w', '--window_length')
parser.add_argument('-f', '--horizon')
parser.add_argument('-b', '--batch_size')
parser.add_argument('-s', '--shuffle')
parser.add_argument('-e', '--epochs')
parser.add_argument('-l', '--latent_dimension')
parser.add_argument('-n', '--hidden_layers')
parser.add_argument('-d', '--hidden_dimension')
parser.add_argument('-a', '--hidden_activation')
parser.add_argument('-t', '--train_val_split')
parser.add_argument('-u', '--val_test_split')
args = parser.parse_args()
argv = vars(args)

WINDOW_LENGTH = int(argv['window_length'])
HORIZON = int(argv['horizon'])
BATCH_SIZE = int(argv['batch_size'])
SHUFFLE = bool(argv['shuffle'])
EPOCHS = int(argv['epochs'])
LATENT_DIMENSION = int(argv['latent_dimension'])
HIDDEN_LAYERS = int(argv['hidden_layers'])
HIDDEN_DIMENSION = int(argv['hidden_dimension'])
HIDDEN_ACTIVATION = argv['hidden_activation']
TRAIN_VAL_SPLIT = int(argv['train_val_split'])
VAL_TEST_SPLIT = int(argv['val_test_split'])

# Import data
response = requests.get('https://localhost:5001/api/Kse/GetWithWeather', verify=False)
df = pd.read_json(response.text)
df.set_index('date', inplace=True)

# Add day and year signal
create_day_year_signal(df)

# Scale data from 0 to 1
scaler = MinMaxScaler((0, 1))
data_scaled = scaler.fit_transform(df)
data_frame = pd.DataFrame(data_scaled, columns=df.columns, index=df.index)

# Auxiliary constants
n_total_features = len(data_frame.columns)
n_aleatoric_features = len(['power'])
n_deterministic_features = n_total_features - n_aleatoric_features

# Splitting dataset into train/val/test
n = len(data_frame)
training_data = data_frame[0:int(n*TRAIN_VAL_SPLIT*0.01)]
validation_data = data_frame[int(n*TRAIN_VAL_SPLIT*0.01):int(n*VAL_TEST_SPLIT*0.01)]
test_data = data_frame[int(n*VAL_TEST_SPLIT*0.01):]

training_windowed = create_dataset(training_data,
                                   n_deterministic_features,
                                   WINDOW_LENGTH,
                                   HORIZON,
                                   BATCH_SIZE,
                                   SHUFFLE)

validation_windowed = create_dataset(validation_data,
                                     n_deterministic_features,
                                     WINDOW_LENGTH,
                                     HORIZON,
                                     BATCH_SIZE,
                                     SHUFFLE)

test_windowed = create_dataset(test_data,
                               n_deterministic_features,
                               WINDOW_LENGTH,
                               HORIZON,
                               batch_size=1,
                               shuffle=SHUFFLE)

# First branch of the net is an lstm which finds an embedding for the past
past_inputs = tf.keras.Input(
    shape=(WINDOW_LENGTH, n_total_features), name='past_inputs')

# Encoding the past
encoder = tf.keras.layers.LSTM(LATENT_DIMENSION, return_state=True)
encoder_outputs, state_h, state_c = encoder(past_inputs)

future_inputs = tf.keras.Input(
    shape=(HORIZON, n_deterministic_features), name='future_inputs')
    
# Combining future inputs with recurrent branch output
decoder_lstm = tf.keras.layers.LSTM(LATENT_DIMENSION, return_sequences=True)
x = decoder_lstm(future_inputs,
                 initial_state=[state_h, state_c])

# Adding hidden layers
for i in range(HIDDEN_LAYERS):
    x = tf.keras.layers.Dense(HIDDEN_DIMENSION, activation=HIDDEN_ACTIVATION)(x)

output = tf.keras.layers.Dense(1, activation='relu')(x)

model = tf.keras.models.Model(
    inputs=[past_inputs, future_inputs], outputs=output)

optimizer = tf.keras.optimizers.Adam()
loss = tf.keras.losses.Huber()
metrics = [tf.metrics.MeanAbsoluteError(), tf.metrics.MeanAbsolutePercentageError(), tf.metrics.MeanSquaredError(), tf.metrics.RootMeanSquaredError()]
model.compile(loss=loss, optimizer=optimizer, metrics=metrics)

# Train the model
history = model.fit(training_windowed, epochs=EPOCHS,
                    validation_data=validation_windowed)

# Evauluate the model on test data
model.evaluate(test_windowed)

# Download forecast
response2 = requests.get('https://localhost:5001/api/Weather/GetFuture', verify=False, params={
    'startDate': df.tail(1).index,
    'days': np.math.ceil(HORIZON/24)
  })
df2 = pd.read_json(response2.text)
df2.set_index('date', inplace=True)

# Add day and year signal
create_day_year_signal(df2)

# Scale data from 0 to 1
df2.insert(0, 'power', '0')
data_scaled2 = scaler.transform(df2)
data_frame2 = pd.DataFrame(data_scaled2, columns=df2.columns, index=df2.index)
data_frame2.drop('power', inplace=True, axis=1)

# Prepare window to predict
predicted_data = pd.concat([data_frame.iloc[-1*WINDOW_LENGTH:], data_frame2])
predicted_windowed = create_dataset(predicted_data,
                               n_deterministic_features,
                               WINDOW_LENGTH,
                               HORIZON,
                               batch_size=1,
                               shuffle=False)

# Make a prediction for 1 window (1 step into future)
(past, future), truth = predicted_windowed.get_single_element()
prediction_value = scaler.data_min_[0] + model.predict((past,future))*(scaler.data_max_[0] - scaler.data_min_[0])
prediction_table = pd.DataFrame.from_records(prediction_value[0], columns=['power'], index=data_frame2.index)

# Add 1 known value on top
plot_prediction = pd.concat([df[-1:], prediction_table])

# Post the prediction back to website
send_data = plot_prediction.reset_index()[['date', 'power']].astype({"power": int}).to_json(orient="records", date_format="iso")
req = requests.post("https://localhost:5001/api/Kse/PostPredictions", verify=False, json = send_data)

if req.status_code == 200:
    exit(0)
else:
    exit(req.status_code)